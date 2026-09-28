using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Chebyss.Battle;

public class BattleBootstrapper : MonoBehaviour
{
    [Header("盤面設定")]
    [SerializeField] private int boardWidth = 8;
    [SerializeField] private int boardHeight = 8;
    [SerializeField] private Vector2Int kingStartPosition = new Vector2Int(4, 0);
    [SerializeField] private int deploymentZoneHeight = 3;
    [SerializeField] private int kingHp = 20;

    [Header("プレイヤー駒パターン（SOアセットを割り当てる）")]
    [SerializeField] private KnightMovementPatternSO knightPattern;
    [SerializeField] private RookMovementPatternSO rookPattern;
    [SerializeField] private BishopMovementPatternSO bishopPattern;

    [Header("駒プレハブ（Assets/Prefabs/Piece/）")]
    [SerializeField] private GameObject knightPrefab; // Knight_Prefab
    [SerializeField] private GameObject rookPrefab;   // Rook_Prefab
    [SerializeField] private GameObject bishopPrefab; // Bishop_Prefab
    [SerializeField] private GameObject kingPrefab;   // King_Prefab
    [SerializeField] private GameObject pawnPrefab;   // Pawn_Prefab（敵の仮の見た目として流用）

    [Header("配置フェーズの待機位置（盤面外の仮置き場）")]
    [SerializeField] private Vector3 knightStagingPosition = new Vector3(-5.5f, 0.25f, -2.5f);
    [SerializeField] private Vector3 rookStagingPosition   = new Vector3(-5.5f, 0.25f, -1.5f);
    [SerializeField] private Vector3 bishopStagingPosition = new Vector3(-5.5f, 0.25f, -0.5f);

    [Header("参照")]
    [SerializeField] private BoardGenerator boardGenerator;

    [Header("状態異常の表示（駒の頭上のSTUN/SLOWラベル）")]
    [SerializeField] private StatusIndicatorSettings statusIndicatorSettings = new StatusIndicatorSettings();

    [Header("敵パターン（SOアセットを割り当てる）")]
    [SerializeField] private StraightAdvanceMovementPatternSO bossPattern;
    [SerializeField] private ChaseNearestMovementPatternSO scoutPattern;
    [SerializeField] private RetreatMovementPatternSO retreaterPattern;

    [Header("敵の初期配置（プロトタイプ用の固定データ。フロア/呪縛レベル未実装のための仮対応）")]
    [SerializeField] private Vector2Int bossStartPosition = new Vector2Int(4, 7);
    [SerializeField] private Vector2Int[] scoutStartPositions =
        { new Vector2Int(2, 7), new Vector2Int(6, 7) };
    [SerializeField] private int bossHp = 30;
    [SerializeField] private int bossAttack = 6;
    [SerializeField] private int scoutHp = 8;
    [SerializeField] private int scoutAttack = 3;

    private GameObject knightInstance, rookInstance, bishopInstance;

    public BoardState Board { get; private set; }
    public PlacementController Placement { get; private set; }
    public BattleTurnController TurnController { get; private set; }
    public BattleVisualSync VisualSync { get; private set; }

    void Awake()
    {
        Board = new BoardState(boardWidth, boardHeight, kingStartPosition);

        boardGenerator.EnsureGenerated();

        VisualSync = gameObject.AddComponent<BattleVisualSync>();
        VisualSync.Initialize(boardGenerator, statusIndicatorSettings);

        var king = PieceSnapshotFactory.Create(
            PieceType.King, isPlayerSide: true, position: kingStartPosition,
            currentHp: kingHp, maxHp: kingHp, attack: 0, statModifiers: null, statusEffects: null);
        Board.PlacePiece(king);
        SpawnFixedStatVisual(kingPrefab, kingStartPosition, king.instanceId, kingHp);

        PlaceFixedTestEnemies();

        knightInstance = Instantiate(knightPrefab, knightStagingPosition, knightPrefab.transform.rotation);
        rookInstance   = Instantiate(rookPrefab, rookStagingPosition, rookPrefab.transform.rotation);
        bishopInstance = Instantiate(bishopPrefab, bishopStagingPosition, bishopPrefab.transform.rotation);

        var knightData = knightInstance.GetComponentInChildren<PieceData>(true);
        var rookData   = rookInstance.GetComponentInChildren<PieceData>(true);
        var bishopData = bishopInstance.GetComponentInChildren<PieceData>(true);

        knightData.SetupInitialStats();
        rookData.SetupInitialStats();
        bishopData.SetupInitialStats();

        var knightSnapshot = knightData.CreateSnapshot(Vector2Int.zero);
        var rookSnapshot   = rookData.CreateSnapshot(Vector2Int.zero);
        var bishopSnapshot = bishopData.CreateSnapshot(Vector2Int.zero);

        var roster = new List<PieceSnapshot> { knightSnapshot, rookSnapshot, bishopSnapshot };
        Placement = new PlacementController(Board, roster, deploymentZoneHeight);

        SetupDraggable(knightInstance, knightStagingPosition, knightSnapshot.instanceId);
        SetupDraggable(rookInstance, rookStagingPosition, rookSnapshot.instanceId);
        SetupDraggable(bishopInstance, bishopStagingPosition, bishopSnapshot.instanceId);

        HighlightDeploymentZone();
    }

    // Awake内でInstantiateした駒のPieceData.Start()（SetupInitialStats）は
    // Awake完了後に走り、HP等を旧設計のフォールバック値で上書きしてしまう。
    // 1フレーム待ってから同期することで、BoardState（唯一の真実）の値が最終的な表示になる。
    private IEnumerator Start()
    {
        yield return null;
        VisualSync.SyncAll(Board);
    }

    // キング・敵専用：SetupInitialStats()の駒種別フォールバックは新設計の数値と無関係なため使わず、
    // Inspectorで設定した値を直接maxHp/currentHpへ書き込む。
    private void SpawnFixedStatVisual(GameObject prefab, Vector2Int startPosition, int instanceId, int hp)
    {
        var tileWorldPos = boardGenerator.GetTile(startPosition).transform.position;
        var instance = Instantiate(prefab, tileWorldPos, prefab.transform.rotation);

        var pieceData = instance.GetComponentInChildren<PieceData>(true);
        pieceData.maxHp = hp;
        pieceData.currentHp = hp;

        var link = instance.AddComponent<PieceBoardLink>();
        link.InstanceId = instanceId;
        link.GridPosition = startPosition;
        link.GroundedHeightOffset = PieceBoardLink.ComputeGroundedHeightOffset(instance.transform);
        instance.transform.position = new Vector3(tileWorldPos.x, tileWorldPos.y + link.GroundedHeightOffset, tileWorldPos.z);

        VisualSync.Register(link);
    }

    private void SetupDraggable(GameObject pieceInstance, Vector3 stagingPosition, int instanceId)
    {
        var draggable = pieceInstance.AddComponent<PlacementDraggable>();
        draggable.Initialize(Placement, boardGenerator, VisualSync, stagingPosition);

        var link = pieceInstance.GetComponent<PieceBoardLink>();
        link.InstanceId = instanceId;
        // ここではまだRegisterしない（配置完了前のためBoardState上に存在せず、
        // SyncAllに「撃破された」と誤認されてしまう）。実際の登録はPlacementDraggable側で、
        // 配置に成功した瞬間に行う。
    }

    private void PlaceFixedTestEnemies()
    {
        var boss = PieceSnapshotFactory.Create(
            PieceType.Pawn, isPlayerSide: false, position: bossStartPosition,
            currentHp: bossHp, maxHp: bossHp, attack: bossAttack, statModifiers: null, statusEffects: null,
            enemyArchetype: EnemyArchetype.Boss);
        Board.PlacePiece(boss);
        SpawnFixedStatVisual(pawnPrefab, bossStartPosition, boss.instanceId, bossHp);

        foreach (var pos in scoutStartPositions)
        {
            var scout = PieceSnapshotFactory.Create(
                PieceType.Pawn, isPlayerSide: false, position: pos,
                currentHp: scoutHp, maxHp: scoutHp, attack: scoutAttack, statModifiers: null, statusEffects: null,
                enemyArchetype: EnemyArchetype.Scout);
            Board.PlacePiece(scout);
            SpawnFixedStatVisual(pawnPrefab, pos, scout.instanceId, scoutHp);
        }
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.enterKey.wasPressedThisFrame) return;

        if (TurnController == null)
        {
            if (!Placement.IsComplete)
            {
                Debug.Log("[BattleBootstrapper] まだ配置が完了していません。3駒すべてを自陣に配置してください。");
                return;
            }

            StartBattlePhase();
            Debug.Log($"[BattleBootstrapper] バトル開始。現在のフェーズ: {TurnController.CurrentPhase}");
        }
        else if (TurnController.CurrentPhase == BattlePhase.PlayerTurn)
        {
            TurnController.EndPlayerTurn();
            VisualSync.SyncAll(Board);
            Debug.Log($"[BattleBootstrapper] ターン終了。現在のフェーズ: {TurnController.CurrentPhase} / ターン数: {Board.CurrentTurn}");
        }
    }

    public void StartBattlePhase()
    {
        if (!Placement.IsComplete) return;

        ClearDeploymentZoneHighlight();

        DisableDraggable(knightInstance);
        DisableDraggable(rookInstance);
        DisableDraggable(bishopInstance);

        var playerRules = new Dictionary<PieceType, IMovementAttackRule>
        {
            { PieceType.Knight, new KnightDashRule() },
            { PieceType.Rook,   new RookLineRule() },
            { PieceType.Bishop, new BishopLineRule() },
        };
        var playerPatterns = new Dictionary<PieceType, PieceMovementPatternSO>
        {
            { PieceType.Knight, knightPattern },
            { PieceType.Rook,   rookPattern },
            { PieceType.Bishop, bishopPattern },
        };

        var enemyRules = new Dictionary<EnemyArchetype, IEnemyMoveRule>
        {
            { EnemyArchetype.Boss,      new StraightAdvanceMoveRule() },
            { EnemyArchetype.Scout,     new ChaseNearestMoveRule() },
            { EnemyArchetype.Retreater, new RetreatMoveRule() },
        };
        var enemyPatterns = new Dictionary<EnemyArchetype, EnemyMovementPatternSO>
        {
            { EnemyArchetype.Boss,      bossPattern },
            { EnemyArchetype.Scout,     scoutPattern },
            { EnemyArchetype.Retreater, retreaterPattern },
        };

        var enemyTurnProcessor = new EnemyTurnProcessor(enemyRules, enemyPatterns);
        TurnController = new BattleTurnController(Board, enemyTurnProcessor, playerRules, playerPatterns);
    }

    private void DisableDraggable(GameObject pieceInstance)
    {
        var draggable = pieceInstance.GetComponent<PlacementDraggable>();
        if (draggable != null) Destroy(draggable);
    }

    private void HighlightDeploymentZone()
    {
        for (int x = 0; x < boardWidth; x++)
            for (int y = 0; y < deploymentZoneHeight; y++)
                boardGenerator.GetTile(new Vector2Int(x, y))?.SetHighlight(HighlightType.Movable);
    }

    private void ClearDeploymentZoneHighlight()
    {
        for (int x = 0; x < boardWidth; x++)
            for (int y = 0; y < deploymentZoneHeight; y++)
                boardGenerator.GetTile(new Vector2Int(x, y))?.SetHighlight(HighlightType.None);
    }
}
