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

    [Header("標準配置（最初のバトルと、記憶のない駒の配置）")]
    [SerializeField] private Vector2Int pawnStandardPosition = new Vector2Int(4, 2);
    [SerializeField] private Vector2Int knightStandardPosition = new Vector2Int(2, 1);
    [SerializeField] private Vector2Int bishopStandardPosition = new Vector2Int(3, 1);
    [SerializeField] private Vector2Int rookStandardPosition = new Vector2Int(5, 1);

    [Header("プレイヤー駒の基礎ステータス（新スケール）")]
    [SerializeField] private int pawnHp = 10;
    [SerializeField] private int pawnAttack = 3;
    [SerializeField] private int knightHp = 12;
    [SerializeField] private int knightAttack = 4;
    [SerializeField] private int rookHp = 14;
    [SerializeField] private int rookAttack = 4;
    [SerializeField] private int bishopHp = 10;
    [SerializeField] private int bishopAttack = 3;

    // 駒種ごとの生成情報（プレハブ・待機位置・基礎HP・基礎攻撃力）
    private struct PlayerPieceSpec
    {
        public PieceType type;
        public GameObject prefab;
        public Vector3 stagingPosition;
        public int hp;
        public int attack;
    }

    private readonly List<GameObject> playerPieceInstances = new();

    private EncounterPlan currentEncounter;

    // 駒種ごとのルール・パターンの対応表（配置プレビューとバトルの両方で使う）
    private Dictionary<PieceType, IMovementAttackRule> playerRules;
    private Dictionary<PieceType, PieceMovementPatternSO> playerPatterns;

    // 名簿の駒種ごとのスナップショット（配置プレビューが、未配置の駒の情報として使う）
    private readonly Dictionary<PieceType, PieceSnapshot> rosterSnapshots = new();

    private PlacementRangePreview rangePreview;
    private PlacementInputController placementInput;

    // 名簿の駒種ごとのPlacementDraggable（クリック配置が配置先の駒を引くために使う）
    private readonly Dictionary<PieceType, PlacementDraggable> draggablesByType = new();
    private bool victoryCommitted;

    // 名簿に入れた駒のinstanceIdと駒種の対応（バトル後に「倒れた駒」を判定するために保持する）
    private readonly List<(int instanceId, PieceType type)> rosterEntries = new();
    private readonly List<PieceType> rosterTypes = new();

    public BoardState Board { get; private set; }
    public PlacementController Placement { get; private set; }
    public BattleTurnController TurnController { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public LoadoutPanelUI Loadout { get; private set; }
    public RunEndPanelUI RunEnd { get; private set; }

    // 配置フェーズ中はtrue、StartBattlePhaseの後はfalse
    public bool IsPlacementPhase { get; private set; } = true;

    public PlacementInputController PlacementInput => placementInput;
    public KeyboardCursorController KeyboardCursor { get; private set; }

    // 名簿の駒種（生成した順。墓地の駒は含まれない）
    public IReadOnlyList<PieceType> RosterTypes => rosterTypes;

    // 名簿の駒のPlacementDraggable（配置フェーズ中のみ有効）
    public bool TryGetDraggable(PieceType type, out PlacementDraggable draggable)
    {
        return draggablesByType.TryGetValue(type, out draggable) && draggable != null;
    }

    // 階数表示・ボス階判定・ランクリア判定用
    public int TotalFloors => encounterSettings.finalFloor;
    public bool IsFinalFloor => RunState.CurrentFloor >= encounterSettings.finalFloor;
    public int BossFloorInterval => encounterSettings.bossFloorInterval;
    public int FinalFloor => encounterSettings.finalFloor;

    // 直近のPrepareNextFloorで確定した、このバトルで倒れた駒（無ければ空）
    public IReadOnlyList<PieceType> LastCasualties { get; private set; } = new List<PieceType>();

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

        BuildPlayerRuleTables();
        rangePreview = gameObject.AddComponent<PlacementRangePreview>();
        rangePreview.Initialize(this, boardGenerator);

        placementInput = gameObject.AddComponent<PlacementInputController>();
        placementInput.Initialize(this, boardGenerator, rangePreview);

        KeyboardCursor = gameObject.AddComponent<KeyboardCursorController>();
        KeyboardCursor.Initialize(this, boardGenerator, rangePreview);

        if (uiFontAsset == null)
            Debug.LogWarning("[BattleBootstrapper] Ui Font Asset がInspectorで未割り当てです。装備画面の日本語が表示されません。");

        Loadout = gameObject.AddComponent<LoadoutPanelUI>();
        Loadout.Initialize(this, uiFontAsset);

        RunEnd = gameObject.AddComponent<RunEndPanelUI>();
        RunEnd.Initialize(this, uiFontAsset);

        var runHud = gameObject.AddComponent<RunHudUI>();
        runHud.Initialize(this, uiFontAsset);

        var controls = gameObject.AddComponent<BattleControlsUI>();
        controls.Initialize(this, uiFontAsset);

        var king = PieceSnapshotFactory.Create(
            PieceType.King, isPlayerSide: true, position: kingStartPosition,
            currentHp: kingHp, maxHp: kingHp, attack: 0, statModifiers: null, statusEffects: null);
        Board.PlacePiece(king);
        SpawnFixedStatVisual(kingPrefab, kingStartPosition, king.instanceId, kingHp);

        currentEncounter = RunState.NextEncounter ?? BuildEncounter(RunState.CurrentFloor);
        RunState.NextEncounter = null;
        PlaceEnemiesFromPlan(currentEncounter);

        var roster = new List<PieceSnapshot>();
        var draggableSetups = new List<(GameObject instance, Vector3 staging, int instanceId)>();

        foreach (var spec in BuildPlayerPieceSpecs())
        {
            // 墓地の駒は生成せず、名簿にもドラッグ設定にも含めない
            if (RunState.IsPieceDead(spec.type)) continue;

            var instance = Instantiate(spec.prefab, spec.stagingPosition, spec.prefab.transform.rotation);
            var data = instance.GetComponentInChildren<PieceData>(true);

            data.SetupInitialStats();

            // SetupInitialStats()は旧スケール（HP1000〜、ATK200〜）の値を入れるため、
            // CreateSnapshot()の前に新スケールで上書きする（BoardStateへ渡る値の唯一の入口）。
            // HPは毎バトル全回復（基礎値と装備からスナップショットが作られる）。
            ApplyPlayerBaseStats(data, spec.hp, spec.attack);

            // RunStateの装備を適用する（CreateSnapshotがequippedItemsを読んでBoardStateへ反映する）
            ApplyRunEquipment(data, spec.type);

            var snapshot = data.CreateSnapshot(Vector2Int.zero);
            roster.Add(snapshot);
            rosterEntries.Add((snapshot.instanceId, spec.type));
            rosterTypes.Add(spec.type);
            rosterSnapshots[spec.type] = snapshot;
            playerPieceInstances.Add(instance);
            draggableSetups.Add((instance, spec.stagingPosition, snapshot.instanceId));
        }

        // 配置完了条件は名簿の駒数に追従する（駒が減っても配置を完了してバトルを開始できる）
        Placement = new PlacementController(Board, roster, deploymentZoneHeight);

        for (int i = 0; i < draggableSetups.Count; i++)
        {
            var setup = draggableSetups[i];
            draggablesByType[rosterEntries[i].type] = SetupDraggable(setup.instance, setup.staging, setup.instanceId);
        }

        AutoPlaceRoster(draggableSetups);

        HighlightDeploymentZone();
    }

    // プレイヤー駒のHP・攻撃力を新スケールで上書きする。attackIntervalなど他の項目は変更しない。
    private List<PlayerPieceSpec> BuildPlayerPieceSpecs()
    {
        return new List<PlayerPieceSpec>
        {
            new PlayerPieceSpec { type = PieceType.Knight, prefab = knightPrefab,     stagingPosition = knightStagingPosition, hp = knightHp, attack = knightAttack },
            new PlayerPieceSpec { type = PieceType.Rook,   prefab = rookPrefab,       stagingPosition = rookStagingPosition,   hp = rookHp,   attack = rookAttack },
            new PlayerPieceSpec { type = PieceType.Bishop, prefab = bishopPrefab,     stagingPosition = bishopStagingPosition, hp = bishopHp, attack = bishopAttack },
            new PlayerPieceSpec { type = PieceType.Pawn,   prefab = playerPawnPrefab, stagingPosition = pawnStagingPosition,   hp = pawnHp,   attack = pawnAttack },
        };
    }

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
    // 勝利の確定（このバトルで1回だけ実行される）：
    // 倒れた駒を確定し（装備は所持品へ戻る）、ラン統計に今回の勝利を記録する。
    public void CommitVictory()
    {
        if (victoryCommitted) return;
        victoryCommitted = true;

        // このバトルで倒れた駒を確定する（盤面に存在しない名簿の駒）。装備は所持品へ戻る。
        var casualties = new List<PieceType>();
        foreach (var entry in rosterEntries)
        {
            if (Board.TryFindPieceById(entry.instanceId, out _, out _, out _)) continue;

            casualties.Add(entry.type);
            RunState.MarkPieceDead(entry.type);
        }
        LastCasualties = casualties;

        int enemyCount = currentEncounter != null ? currentEncounter.enemies.Count : 0;
        int itemCount = TurnController != null ? TurnController.LootLedger.Collected.Count : 0;
        RunState.RecordVictory(enemyCount, itemCount, casualties.Count);
    }

    // 次の階へ進む準備（階数を進め、次の敵計画を作る）。倒れた駒の確定はCommitVictoryで行う。
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

    // 駒種ごとのルール・パターンの対応表。ルールは状態を持たないため、配置フェーズから使い回せる。
    private void BuildPlayerRuleTables()
    {
        playerRules = new Dictionary<PieceType, IMovementAttackRule>
        {
            { PieceType.Knight, new KnightDashRule() },
            { PieceType.Rook,   new RookLineRule() },
            { PieceType.Bishop, new BishopLineRule() },
            { PieceType.Pawn,   new PawnAdvanceRule() },
        };
        playerPatterns = new Dictionary<PieceType, PieceMovementPatternSO>
        {
            { PieceType.Knight, knightPattern },
            { PieceType.Rook,   rookPattern },
            { PieceType.Bishop, bishopPattern },
            { PieceType.Pawn,   pawnPattern },
        };
    }

    // 配置プレビュー用：未配置の駒でも範囲を計算できるよう、名簿のスナップショットとルールを返す
    public bool TryGetPlacementPreviewData(
        PieceType type, out PieceSnapshot snapshot, out IMovementAttackRule rule, out PieceMovementPatternSO pattern)
    {
        rule = null;
        pattern = null;
        if (!rosterSnapshots.TryGetValue(type, out snapshot)) return false;

        return playerRules.TryGetValue(type, out rule) && playerPatterns.TryGetValue(type, out pattern);
    }

    // ---- 前回配置の記憶と自動配置 ----

    private Vector2Int GetStandardPlacement(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn:   return pawnStandardPosition;
            case PieceType.Knight: return knightStandardPosition;
            case PieceType.Bishop: return bishopStandardPosition;
            case PieceType.Rook:   return rookStandardPosition;
            default:               return kingStartPosition + Vector2Int.up;
        }
    }

    // 名簿の各駒を自動配置する。前回の配置（なければ標準配置）が使えなければ、最寄りの空きマスにする。
    // それでも置けなければ未配置のまま（プレイヤーが手で置く）。
    private void AutoPlaceRoster(List<(GameObject instance, Vector3 staging, int instanceId)> setups)
    {
        for (int i = 0; i < setups.Count; i++)
        {
            var type = rosterEntries[i].type;
            var draggable = setups[i].instance.GetComponent<PlacementDraggable>();
            if (draggable == null) continue;

            var desired = RunState.TryGetLastPlacement(type, out var last) ? last : GetStandardPlacement(type);

            Vector2Int cell;
            if (IsFreeDeploymentCell(desired))
            {
                cell = desired;
            }
            else
            {
                var nearest = FindNearestFreeDeploymentCell(desired);
                if (!nearest.HasValue) continue;
                cell = nearest.Value;
            }

            draggable.PlaceAt(cell);
        }
    }

    // 盤面内・自陣エリア内で、他の駒や敵がいないマス
    private bool IsFreeDeploymentCell(Vector2Int pos)
    {
        return Placement.IsInDeploymentZone(pos) && !Board.IsOccupied(pos);
    }

    // 自陣エリア内の最寄りの空きマス（チェビシェフ距離。同距離ならx→y順）
    private Vector2Int? FindNearestFreeDeploymentCell(Vector2Int origin)
    {
        Vector2Int? best = null;
        int bestDistance = int.MaxValue;

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < deploymentZoneHeight; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!IsFreeDeploymentCell(pos)) continue;

                int distance = Mathf.Max(Mathf.Abs(pos.x - origin.x), Mathf.Abs(pos.y - origin.y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pos;
                }
            }
        }

        return best;
    }

    // 配置完了時に、盤面上の味方駒（キング除く）の駒種とマスを記憶する
    private void SavePlacementToRunState()
    {
        var placement = new List<KeyValuePair<PieceType, Vector2Int>>();
        foreach (var pos in Board.GetAllPlayerPiecePositionsOrdered())
        {
            if (Board.TryGetPlayerPiece(pos, out var piece) && piece.pieceType != PieceType.King)
                placement.Add(new KeyValuePair<PieceType, Vector2Int>(piece.pieceType, pos));
        }
        RunState.SavePlacement(placement);
    }

    private PlacementDraggable SetupDraggable(GameObject pieceInstance, Vector3 stagingPosition, int instanceId)
    {
        var draggable = pieceInstance.AddComponent<PlacementDraggable>();
        draggable.Initialize(Placement, boardGenerator, VisualSync, stagingPosition, rangePreview, placementInput);

        var link = pieceInstance.GetComponent<PieceBoardLink>();
        link.InstanceId = instanceId;
        // ここではまだRegisterしない（配置完了前のためBoardState上に存在せず、
        // SyncAllに「撃破された」と誤認されてしまう）。実際の登録はPlacementDraggable側で、
        // 配置に成功した瞬間に行う。

        return draggable;
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

        // 配置フェーズの終了：プレビューを先に消してから自陣エリアの強調を消す
        // （プレビューが上書き前の強調を復元してしまわないようにするため）
        IsPlacementPhase = false;
        placementInput.Deselect(); // 選択中の駒の上下動・プレビューを止める
        rangePreview.Clear();
        SavePlacementToRunState();

        ClearDeploymentZoneHighlight();

        foreach (var instance in playerPieceInstances)
            DisableDraggable(instance);

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
