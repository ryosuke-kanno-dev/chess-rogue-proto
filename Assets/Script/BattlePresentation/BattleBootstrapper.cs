using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private PawnMovementPatternSO pawnPattern;

    [Header("駒プレハブ（Assets/Prefabs/Piece/）")]
    [SerializeField] private GameObject knightPrefab; // Knight_Prefab
    [SerializeField] private GameObject rookPrefab;   // Rook_Prefab
    [SerializeField] private GameObject bishopPrefab; // Bishop_Prefab
    [SerializeField] private GameObject kingPrefab;   // King_Prefab
    [SerializeField] private GameObject bossPrefab;   // King_Prefab（ボスの仮の見た目として流用。駒としてのKingとは別インスタンス）
    [SerializeField] private GameObject pawnPrefab;   // Pawn_Prefab（スカウトの仮の見た目として流用）
    [SerializeField] private GameObject playerPawnPrefab; // Pawn_Prefab（プレイヤー側のポーン本体として使用）

    [Header("配置フェーズの待機位置（盤面外の仮置き場）")]
    [SerializeField] private Vector3 knightStagingPosition = new Vector3(-5.5f, 0.25f, -2.5f);
    [SerializeField] private Vector3 rookStagingPosition   = new Vector3(-5.5f, 0.25f, -1.5f);
    [SerializeField] private Vector3 bishopStagingPosition = new Vector3(-5.5f, 0.25f, -0.5f);
    [SerializeField] private Vector3 pawnStagingPosition   = new Vector3(-5.5f, 0.25f, 0.5f);

    [Header("参照")]
    [SerializeField] private BoardGenerator boardGenerator;
    [SerializeField] private EnemyIntentDisplay intentDisplay;

    [Header("状態異常の表示（駒の頭上のSTUN/SLOWラベル）")]
    [SerializeField] private StatusIndicatorSettings statusIndicatorSettings = new StatusIndicatorSettings();

    [Header("ポーンの経験値・進化（仮数値。Phase2/3の数値スケール統一時に本調整する）")]
    [SerializeField] private PawnGrowthRules pawnGrowthRules = new PawnGrowthRules();

    [Header("敵パターン（SOアセットを割り当てる）")]
    [SerializeField] private StraightAdvanceMovementPatternSO bossPattern;
    [SerializeField] private ChaseNearestMovementPatternSO scoutPattern;
    [SerializeField] private RetreatMovementPatternSO retreaterPattern;

    [Header("敵の見た目（味方の白い駒と見分けやすくするための色分け）")]
    [SerializeField] private Color enemyTintColor = new Color(0.55f, 0.15f, 0.15f);
    [SerializeField] private Color retreaterTintColor = new Color(1f, 0.85f, 0.3f);

    [Header("階ごとの敵生成（暫定）")]
    [SerializeField] private EncounterGeneratorSettings encounterSettings = new EncounterGeneratorSettings();

    [Header("敵の初期配置（プロトタイプ用の固定データ。フロア/呪縛レベル未実装のための仮対応）")]
    // 未使用（EncounterGeneratorが配置を決める）
    [SerializeField] private Vector2Int bossStartPosition = new Vector2Int(4, 7);
    // 未使用（EncounterGeneratorが配置を決める）
    [SerializeField] private Vector2Int[] scoutStartPositions =
        { new Vector2Int(2, 7), new Vector2Int(6, 7) };
    [SerializeField] private int bossHp = 30;
    [SerializeField] private int bossAttack = 6;
    [SerializeField] private int scoutHp = 8;
    [SerializeField] private int scoutAttack = 3;

    [Header("装備画面のUI")]
    [SerializeField] private TMP_FontAsset uiFontAsset;

    [Header("感知レベル（-1 = 装備から算出 / 0〜3 = 確認用にそのレベルへ固定）")]
    [Range(-1, 3)]
    [SerializeField] private int senseLevelOverride = -1;

    [Header("プレイヤー駒の基礎ステータス（新スケール）")]
    [SerializeField] private int pawnHp = 10;
    [SerializeField] private int pawnAttack = 3;
    [SerializeField] private int knightHp = 12;
    [SerializeField] private int knightAttack = 4;
    [SerializeField] private int rookHp = 14;
    [SerializeField] private int rookAttack = 4;
    [SerializeField] private int bishopHp = 10;
    [SerializeField] private int bishopAttack = 3;

    private GameObject knightInstance, rookInstance, bishopInstance, pawnInstance;

    public BoardState Board { get; private set; }
    public PlacementController Placement { get; private set; }
    public BattleTurnController TurnController { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public LoadoutPanelUI Loadout { get; private set; }

    // 装備画面の盤面の簡易図が参照する、盤面の読み取り専用情報
    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;
    public Vector2Int KingStartPosition => kingStartPosition;
    public int DeploymentZoneHeight => deploymentZoneHeight;
    public BattleVisualSync VisualSync { get; private set; }

    void Awake()
    {
        Board = new BoardState(boardWidth, boardHeight, kingStartPosition);

        boardGenerator.EnsureGenerated();

        WarnIfPatternMissing(knightPattern, "Knight Pattern");
        WarnIfPatternMissing(rookPattern, "Rook Pattern");
        WarnIfPatternMissing(bishopPattern, "Bishop Pattern");
        WarnIfPatternMissing(pawnPattern, "Pawn Pattern");
        WarnIfOnHitEffectsEmpty(knightPattern, "Knight Pattern");
        WarnIfOnHitEffectsEmpty(bishopPattern, "Bishop Pattern");

        VisualSync = gameObject.AddComponent<BattleVisualSync>();
        VisualSync.Initialize(boardGenerator, statusIndicatorSettings);

        Inventory = RunState.Inventory; // 階をまたいで維持される所持品

        var dropHover = gameObject.AddComponent<DropHoverController>();
        dropHover.Initialize(this, boardGenerator);

        if (uiFontAsset == null)
            Debug.LogWarning("[BattleBootstrapper] Ui Font Asset がInspectorで未割り当てです。装備画面の日本語が表示されません。");

        Loadout = gameObject.AddComponent<LoadoutPanelUI>();
        Loadout.Initialize(this, uiFontAsset);

        var king = PieceSnapshotFactory.Create(
            PieceType.King, isPlayerSide: true, position: kingStartPosition,
            currentHp: kingHp, maxHp: kingHp, attack: 0, statModifiers: null, statusEffects: null);
        Board.PlacePiece(king);
        SpawnFixedStatVisual(kingPrefab, kingStartPosition, king.instanceId, kingHp);

        var encounter = RunState.NextEncounter ?? BuildEncounter(RunState.CurrentFloor);
        RunState.NextEncounter = null;
        PlaceEnemiesFromPlan(encounter);

        knightInstance = Instantiate(knightPrefab, knightStagingPosition, knightPrefab.transform.rotation);
        rookInstance   = Instantiate(rookPrefab, rookStagingPosition, rookPrefab.transform.rotation);
        bishopInstance = Instantiate(bishopPrefab, bishopStagingPosition, bishopPrefab.transform.rotation);
        pawnInstance   = Instantiate(playerPawnPrefab, pawnStagingPosition, playerPawnPrefab.transform.rotation);

        var knightData = knightInstance.GetComponentInChildren<PieceData>(true);
        var rookData   = rookInstance.GetComponentInChildren<PieceData>(true);
        var bishopData = bishopInstance.GetComponentInChildren<PieceData>(true);
        var pawnData   = pawnInstance.GetComponentInChildren<PieceData>(true);

        knightData.SetupInitialStats();
        rookData.SetupInitialStats();
        bishopData.SetupInitialStats();
        pawnData.SetupInitialStats();

        // SetupInitialStats()は旧スケール（HP1000〜、ATK200〜）の値を入れるため、
        // CreateSnapshot()の前に新スケールで上書きする（BoardStateへ渡る値の唯一の入口）。
        ApplyPlayerBaseStats(knightData, knightHp, knightAttack);
        ApplyPlayerBaseStats(rookData, rookHp, rookAttack);
        ApplyPlayerBaseStats(bishopData, bishopHp, bishopAttack);
        ApplyPlayerBaseStats(pawnData, pawnHp, pawnAttack);

        // RunStateの装備を各駒に適用する（CreateSnapshotがequippedItemsを読んでBoardStateへ反映する）
        ApplyRunEquipment(knightData, PieceType.Knight);
        ApplyRunEquipment(rookData, PieceType.Rook);
        ApplyRunEquipment(bishopData, PieceType.Bishop);
        ApplyRunEquipment(pawnData, PieceType.Pawn);

        var knightSnapshot = knightData.CreateSnapshot(Vector2Int.zero);
        var rookSnapshot   = rookData.CreateSnapshot(Vector2Int.zero);
        var bishopSnapshot = bishopData.CreateSnapshot(Vector2Int.zero);
        var pawnSnapshot   = pawnData.CreateSnapshot(Vector2Int.zero);

        var roster = new List<PieceSnapshot> { knightSnapshot, rookSnapshot, bishopSnapshot, pawnSnapshot };
        Placement = new PlacementController(Board, roster, deploymentZoneHeight);

        SetupDraggable(knightInstance, knightStagingPosition, knightSnapshot.instanceId);
        SetupDraggable(rookInstance, rookStagingPosition, rookSnapshot.instanceId);
        SetupDraggable(bishopInstance, bishopStagingPosition, bishopSnapshot.instanceId);
        SetupDraggable(pawnInstance, pawnStagingPosition, pawnSnapshot.instanceId);

        HighlightDeploymentZone();
    }

    // プレイヤー駒のHP・攻撃力を新スケールで上書きする。attackIntervalなど他の項目は変更しない。
    private void ApplyPlayerBaseStats(PieceData data, int hp, int attack)
    {
        data.maxHp = hp;
        data.currentHp = hp;
        data.attack = attack;
    }

    private void ApplyRunEquipment(PieceData data, PieceType type)
    {
        foreach (var item in RunState.GetEquipped(type))
        {
            if (item != null) data.EquipItem(item);
        }
    }

    // ---- 装備画面（LoadoutPanelUI）向けの公開メンバ ----

    public int GetPlayerBaseHp(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return knightHp;
            case PieceType.Rook:   return rookHp;
            case PieceType.Bishop: return bishopHp;
            case PieceType.Pawn:   return pawnHp;
            default:               return 0;
        }
    }

    public int GetPlayerBaseAttack(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return knightAttack;
            case PieceType.Rook:   return rookAttack;
            case PieceType.Bishop: return bishopAttack;
            case PieceType.Pawn:   return pawnAttack;
            default:               return 0;
        }
    }

    public float GetKnightBaseDistanceBonusRate()
    {
        return knightPattern != null ? knightPattern.distanceBonusRate : 0f;
    }

    // senseLevelOverrideが0以上ならその値に固定（確認用）。そうでなければ装備の「感知」の合計から算出する。
    public int GetSenseLevel()
    {
        if (senseLevelOverride >= 0) return senseLevelOverride;
        return Mathf.Clamp(RunState.GetTotalSenseValue(), 0, RunState.MaxSenseLevel);
    }

    public bool IsSenseLevelOverridden => senseLevelOverride >= 0;

    // 敵種ごとの、キングへ隣接したときのダメージ。パターンが未割り当てなら-1。
    public int GetEnemyKingDamage(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Boss:      return bossPattern != null ? bossPattern.kingDamage : -1;
            case EnemyArchetype.Scout:     return scoutPattern != null ? scoutPattern.kingDamage : -1;
            case EnemyArchetype.Retreater: return retreaterPattern != null ? retreaterPattern.kingDamage : -1;
            default:                       return -1;
        }
    }

    // 勝利後、次の階へ進む準備（階数を進め、次の敵計画を作る）
    public void PrepareNextFloor()
    {
        RunState.CurrentFloor++;
        RunState.NextEncounter = BuildEncounter(RunState.CurrentFloor);
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
    private void SpawnFixedStatVisual(GameObject prefab, Vector2Int startPosition, int instanceId, int hp, Color? tintColor = null)
    {
        var tileWorldPos = boardGenerator.GetTile(startPosition).transform.position;
        var instance = Instantiate(prefab, tileWorldPos, prefab.transform.rotation);

        var pieceData = instance.GetComponentInChildren<PieceData>(true);
        pieceData.maxHp = hp;
        pieceData.currentHp = hp;

        // 敵駒を味方（白）と見分けやすくするための色分け。PieceActionStateIndicatorは
        // プレイヤー駒にしか付かないため、ここで競合することはない。
        if (tintColor.HasValue)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.material.color = tintColor.Value;
        }

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

    // 階ごとの敵計画を生成する。seedはラン固定のRunSeedと階数から決まるため、
    // 同じ階なら（装備画面での表示も実際の戦闘も）常に同じ構成になる。
    private EncounterPlan BuildEncounter(int floor)
    {
        var plan = EncounterGenerator.Generate(
            floor, boardWidth, boardHeight, encounterSettings,
            bossHp, bossAttack, scoutHp, scoutAttack,
            allowRetreater: retreaterPattern != null,
            seed: unchecked(RunState.RunSeed * 7919 + floor));

        int bosses = 0, scouts = 0, retreaters = 0;
        foreach (var enemy in plan.enemies)
        {
            switch (enemy.archetype)
            {
                case EnemyArchetype.Boss:      bosses++; break;
                case EnemyArchetype.Scout:     scouts++; break;
                case EnemyArchetype.Retreater: retreaters++; break;
            }
        }
        Debug.Log($"[Encounter] {floor}F: ボス×{bosses} スカウト×{scouts} 逃げる敵×{retreaters}");

        return plan;
    }

    private void PlaceEnemiesFromPlan(EncounterPlan plan)
    {
        foreach (var spawn in plan.enemies)
        {
            var enemy = PieceSnapshotFactory.Create(
                PieceType.King, isPlayerSide: false, position: spawn.position,
                currentHp: spawn.hp, maxHp: spawn.hp, attack: spawn.attack, statModifiers: null, statusEffects: null,
                enemyArchetype: spawn.archetype);
            Board.PlacePiece(enemy);

            // 見た目：ボスはbossPrefab、それ以外（スカウト・逃げる敵）はpawnPrefabを仮の見た目として流用
            var prefab = spawn.archetype == EnemyArchetype.Boss ? bossPrefab : pawnPrefab;
            var tint = spawn.archetype == EnemyArchetype.Retreater ? retreaterTintColor : enemyTintColor;
            SpawnFixedStatVisual(prefab, spawn.position, enemy.instanceId, spawn.hp, tint);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            AdvancePhase();
    }

    // 配置完了→バトル開始、またはプレイヤーターン→ターン終了を状況に応じて実行する。
    // Enterキー（ショートカット）と、新設のBattlePhaseActionButtonの両方から
    // 同じこのメソッド経由で呼び出すことで、処理が2箇所に分かれないようにする。
    public void AdvancePhase()
    {
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
            if (TurnController.CurrentPhase == BattlePhase.PlayerTurn)
                intentDisplay.UpdateIntents(Board, TurnController.EnemyTurnProcessor);
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
        DisableDraggable(pawnInstance);

        var playerRules = new Dictionary<PieceType, IMovementAttackRule>
        {
            { PieceType.Knight, new KnightDashRule() },
            { PieceType.Rook,   new RookLineRule() },
            { PieceType.Bishop, new BishopLineRule() },
            { PieceType.Pawn,   new PawnAdvanceRule() },
        };
        var playerPatterns = new Dictionary<PieceType, PieceMovementPatternSO>
        {
            { PieceType.Knight, knightPattern },
            { PieceType.Rook,   rookPattern },
            { PieceType.Bishop, bishopPattern },
            { PieceType.Pawn,   pawnPattern },
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
        TurnController = new BattleTurnController(
            Board, enemyTurnProcessor, playerRules, playerPatterns, pawnGrowthRules,
            Inventory,
            a => BattleEquipmentGenerator.GenerateRandomEquipment(DropRules.GetQualityTier(a)));

        intentDisplay.UpdateIntents(Board, TurnController.EnemyTurnProcessor);
    }

    private void DisableDraggable(GameObject pieceInstance)
    {
        var draggable = pieceInstance.GetComponent<PlacementDraggable>();
        if (draggable != null) Destroy(draggable);
    }

    // 診断用：onHitEffectsはPieceMovementPatternSOのデフォルトが空リストで、
    // Unityエディタ上でInspectorから手動で要素を追加しない限り空のまま。
    // 空のままだと攻撃してもスタン・スロウが一切付与されず、状態異常ラベルも
    // 永遠に表示されない（表示ロジック側の問題ではない）。攻撃を待たず、
    // 再生直後のConsoleで気づけるようにする。
    // 全駒種共通：パターンSO自体が未割り当てだと、実際に攻撃した瞬間に
    // NullReferenceExceptionとして発覚する（ExecuteAttack内でpattern.xxxへアクセスするため）。
    // 再生直後に気づけるよう先に警告する。
    private static void WarnIfPatternMissing(PieceMovementPatternSO pattern, string fieldLabel)
    {
        if (pattern == null)
            Debug.LogWarning($"[BattleBootstrapper] {fieldLabel} がInspectorで未割り当てです。");
    }

    // ナイト・ビショップ専用：この2駒はOn Hit Effectsが空だと設計上おかしい
    // （スタン・スロウが機能しなくなる）。ルーク・ポーンはOn Hit Effectsが空で
    // 正しいため、この関数の対象に含めない（含めると誤った警告になる）。
    private static void WarnIfOnHitEffectsEmpty(PieceMovementPatternSO pattern, string fieldLabel)
    {
        if (pattern == null) return; // WarnIfPatternMissingで警告済み

        if (pattern.onHitEffects == null || pattern.onHitEffects.Count == 0)
        {
            Debug.LogWarning(
                $"[BattleBootstrapper] {fieldLabel}（{pattern.name}）の On Hit Effects が空です。" +
                "このままでは攻撃しても状態異常が一切付与されません。" +
                "アセットのInspectorで On Hit Effects に要素を追加し、Type・Duration・Magnitude を設定してください。");
        }
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
