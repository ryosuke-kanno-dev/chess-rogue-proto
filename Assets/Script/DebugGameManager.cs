using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum GrowthType
{
  AttackUp,
  HpUp,
  SpeedUp,
  Lifesteal
}

// 課題3【コイン獲得処理の一元化】: Goldがどこから増減したかを表す出所種別。
// DebugGameManager.AddGold(amount, sourceType) の第2引数として使う。
public enum GoldSourceType
{
  Initial,           // ゲーム開始時の初期所持金
  WaveClear,         // ウェーブクリア報酬（富の知識スキル込み）
  WaveChoiceReward,  // 3択フロア選択（弱/中/強）の即時報酬
  EnemyKill,         // 敵撃破報酬
  EquipmentAutoSell, // インベントリ満杯時の装備自動売却
  ShopPurchase,      // ショップでの駒購入（支出）
  ShopReroll,        // ショップの有償リロール（支出）
  ManualHeal,        // Goldでの味方全回復（支出）
  Debug              // デバッグパネルからの付与
}

// 課題1【SO一元化】: WaveChoiceOption クラスの定義は GameConfigSO.cs へ移設しました。
// （GameConfigSO.waveChoiceOptions がこのゲームの3択フロア選択の単一の真実になります）

// ステップ7: 墓地に記録される戦死駒1件分のデータ
[System.Serializable]
public class CemeteryRecord
{
  public string pieceName;
  public PieceType type;
  public int rank;
  public int deathWave;
  public List<CemeteryEquipmentEntry> equipmentLog = new List<CemeteryEquipmentEntry>();
}

// ステップ8: 墓地記録に紐づく装備1個分の結末（回収されたかロストしたか）
[System.Serializable]
public class CemeteryEquipmentEntry
{
  public string itemName;
  public EquipmentRarity rarity;
  public bool wasRecovered;
}

public class DebugGameManager : MonoBehaviour
{
  public static DebugGameManager Instance { get; private set; }

  [Header("フェーズ & ステージ状態")]
  public bool isBattleStarted = false;
  public bool isGameOver = false;
  public string gameResultText = "";
  public int currentWave = 1;
  public int playerHp = 300; // 課題1: 実際の初期値はStart()でgameConfig.initialPlayerHpにより上書きされる（未設定時はこのInspector値を使用）

  [Header("課題1: ゲーム全体設定（ScriptableObject）")]
  [Tooltip("初期Gold・回復コスト・盤面サイズ・スキルツリー係数・エンドレス設定・スコア計算・3択フロア選択肢など、\n駒に依存しないゲーム全体の設定値を一元管理する。未設定の場合は既存のハードコード値へフォールバックする。")]
  [SerializeField] private GameConfigSO gameConfig;

  // 課題【AIパターンのSO管理化】: PieceAIBehaviorSelectorModal等、外部からplayerSelectableAIBehaviors等を
  // 参照する必要がある箇所のための公開アクセサ（gameConfig自体は引き続きprivateのまま、読み取り専用で公開する）
  public GameConfigSO GameConfig => gameConfig;

  [Header("盤面生成（プレハブ方式）")]
  [Tooltip("【新方式・推奨】チェッカー模様の1マス分の共通プレハブ。これを設定した場合、下のlightTileMaterial/darkTileMaterialを用いてチェッカー模様に着色して生成する")]
  [SerializeField] private GameObject tilePrefab;
  [Tooltip("【新方式】tilePrefab使用時の明るいマス用マテリアル")]
  [SerializeField] private Material lightTileMaterial;
  [Tooltip("【新方式】tilePrefab使用時の暗いマス用マテリアル")]
  [SerializeField] private Material darkTileMaterial;
  [Tooltip("【旧方式・互換用】薄い色のマスのプレハブ。tilePrefabが未設定の場合のみ使用される")]
  [SerializeField] private GameObject lightTilePrefab;
  [Tooltip("【旧方式・互換用】濃い色のマスのプレハブ。tilePrefabが未設定の場合のみ使用される")]
  [SerializeField] private GameObject darkTilePrefab;
  [Tooltip("ベンチ用マスのプレハブ（未設定の場合は盤面と同じプレハブ/マテリアルで代用する）")]
  [SerializeField] private GameObject benchTilePrefab;
  [Tooltip("ベンチ用マスのマテリアル（未設定の場合は着色しない＝プレハブそのままの見た目）")]
  [SerializeField] private Material benchTileMaterial;
  [Tooltip("生成したマスを格納する親Transform（未設定の場合はこのGameObject自身の直下に生成）。\n盤面・タイル・駒全体の大きさは、このTransformのlocalScaleのみで調整してください（コード側では倍率を一切掛けません）。")]
  [SerializeField] private Transform boardParent;

  // 課題1: 盤面サイズはgameConfig.boardWidth/boardDepthを単一の真実とする（未設定時は8x8）
  public int BoardWidth => gameConfig != null ? Mathf.Max(2, gameConfig.boardWidth) : 8;
  public int BoardDepth => gameConfig != null ? Mathf.Max(2, gameConfig.boardDepth) : 8;
  // 課題3: プレイヤー駒が配置可能な自陣の行数（盤面手前から数えて何行まで置けるか）
  public int PlayerFrontRowDepth => gameConfig != null ? Mathf.Clamp(gameConfig.playerFrontRowDepth, 1, BoardDepth) : 2;
  // 課題3: 敵駒がスポーンし得る「盤面奥側」の行数
  public int EnemyBackRowDepth => gameConfig != null ? Mathf.Clamp(gameConfig.enemyBackRowDepth, 1, BoardDepth) : 2;

  // 【ベンチ再設計】ベンチは盤面グリッド(BoardWidth/BoardDepth)から完全に独立したパラメータで管理する
  public int BenchSlotCount => gameConfig != null ? Mathf.Max(1, gameConfig.benchSlotCount) : 8;
  public float BenchGapFromBoard => gameConfig != null ? Mathf.Max(0f, gameConfig.benchGapFromBoard) : 1.0f;

  [Header("ステップ29: 3択フロア選択システム（基礎設計）")]
  [Tooltip("gameConfigが未設定、またはgameConfig.waveChoiceOptionsが空の場合に使うフォールバック用の3択データ")]
  [SerializeField]
  private WaveChoiceOption[] waveChoiceOptionsFallback = new WaveChoiceOption[]
  {
    new WaveChoiceOption { label = "弱敵", enemyStatMultiplier = 0.8f, goldReward = 2000, dropRateMultiplier = 1.0f },
    new WaveChoiceOption { label = "中敵", enemyStatMultiplier = 1.0f, goldReward = 4000, dropRateMultiplier = 1.0f },
    new WaveChoiceOption { label = "強敵", enemyStatMultiplier = 1.3f, goldReward = 8000, dropRateMultiplier = 1.5f },
  };

  private float currentWaveEnemyMultiplier = 1f;   // 選択された難易度倍率（EnemyWaveDataSO由来の敵ステータスへ乗算）
  private float currentWaveDropRateMultiplier = 1f; // 選択された難易度による装備ドロップ率倍率
  private bool showWaveChoiceModal = false;
  private bool waveChoiceStatApplied = false; // 課題2: 同一ウェーブ内でのステータス倍率の二重適用を防ぐガード

  [Header("ステップ24: パラメータデータ（ScriptableObject）")]
  [Tooltip("Wave1〜10の敵構成データ。未設定または該当Waveのエントリが無い場合は既存の計算式ロジックへフォールバックする")]
  [SerializeField] private EnemyWaveDataSO enemyWaveData;
  [Tooltip("駒種ごとの基礎ステータス・コスト・ドロップ率データ（PieceDataSOに相当）。未設定または該当駒種のエントリが無い場合は既存のハードコード値へフォールバックする")]
  [SerializeField] private UnitStatusDataSO unitStatusData;
  [Tooltip("合成時3択成長ボーナスのデータ。未設定または該当タイプのエントリが無い場合は既存のハードコード値へフォールバックする")]
  [SerializeField] private GrowthBonusDataSO growthBonusData;
  [Tooltip("★2→★3進化時の育成履歴分岐データ。未設定または該当エントリが無い場合はフレーバー名なしの通常RankUpのみになる")]
  [SerializeField] private EvolutionRuleDataSO evolutionRuleData;
  [Tooltip("異種合成（融合）のレシピデータ。未設定の場合はUI_GetFusionCandidatesが常に空リストを返す")]
  [SerializeField] private FusionRecipeDataSO fusionRecipeData;

  // PieceData.SetupInitialStats()から参照するための公開アクセサ
  public UnitStatusDataSO UnitStatusData => unitStatusData;

  // 課題【AIパターンのSO管理化】: 現在ウェーブのAIBehaviorDataSOをUI表示等から参照するための公開プロパティ。
  // enemyWaveData未設定、または該当ウェーブのエントリが無い、またはaiBehavior未設定の場合はnull（＝バランス型扱い）を返す。
  public AIBehaviorDataSO CurrentWaveAIBehavior =>
    enemyWaveData != null ? enemyWaveData.GetWave(currentWave)?.aiBehavior : null;

  [Header("経済システム")]
  public int gold = 400; // 課題1: 実際の初期値はStart()でgameConfig.initialGoldにより上書きされる（未設定時はこのInspector値を使用）
  private PieceType[] shopItems = new PieceType[3];

  // 課題3【コイン獲得処理の一元化】: Goldの増減は必ずこのAddGoldを経由させる。
  // 呼び出し元ごとにgold += /-= を直接書いていた従来の方式では、
  // 「どこでいくらGoldが動いたか」を横断的に把握・調整することが難しかったため、
  // 単一のメソッド＋出所を表すGoldSourceTypeに一元化する。
  // 実際の金額（waveBaseGoldReward, sellValue各種, goldPerEnemyKill 等）は全てGameConfigSO側で管理し、
  // ここでは受け取った量をgoldへ反映するだけの薄いメソッドに留める。
  public void AddGold(int amount, GoldSourceType sourceType)
  {
    gold += amount;
    if (gold < 0) gold = 0; // 購入等の可否は呼び出し側で事前チェックしている想定だが、念のため下限を保護する

    // 収支ログが欲しくなった場合はここに一箇所追加するだけで全ての出入りを追跡できる
    // Debug.Log($"[Gold] {sourceType}: {(amount >= 0 ? "+" : "")}{amount}G → 所持{gold}G");
  }

  [Header("キング育成・スキルツリー")]
  public int skillPoints = 0;
  public int skillAuraLevel = 0;      // 全体攻撃力バフ
  public int skillEconomyLevel = 0;   // ターン追加ゴールド
  public int skillBarrierLevel = 0;   // 戦闘開始時耐久加算
  private bool showSkillTreeModal = false;

  [Header("ステップ4: ハクスラ装備システム")]
  public List<EquipmentInstance> inventory = new List<EquipmentInstance>();
  private int MaxInventorySlots => gameConfig != null ? Mathf.Max(1, gameConfig.maxInventorySlots) : 8;

  [Header("ステップ5: エンドレスモード & スコア記録")]
  public bool isEndlessMode = false;
  public int totalEnemiesDefeated = 0;
  private int EndlessStartWave => gameConfig != null ? gameConfig.endlessStartWave : 11;
  private int RerollCost => gameConfig != null ? gameConfig.rerollCost : 200; // ステップ30: UIから参照できるようプロパティに切り出し
  private float EndlessScalingRate => gameConfig != null ? gameConfig.endlessScalingRate : 1.18f; // 1ウェーブごとの敵ステータス倍率
  private string endlessAnnounceText = "";
  private float endlessAnnounceTimer = 0f;
  private int finalScore = 0;
  private bool isNewHighScore = false;
  private int ScorePerWave => gameConfig != null ? gameConfig.scorePerWave : 1000;
  private int ScorePerKill => gameConfig != null ? gameConfig.scorePerKill : 100;
  private int ScorePerGold => gameConfig != null ? gameConfig.scorePerGold : 10;
  private int ScorePerHp => gameConfig != null ? gameConfig.scorePerHp : 5;

  private bool isRoundEnding = false;
  private float roundEndTimer = 0f;

  [Header("ステップ6: UI分離 & 倍速/スキップ")]
  public bool showDebugMenu = false;
  private float[] speedOptions = new float[] { 1f, 2f, 4f };
  private int currentSpeedIndex = 0;
  private bool isSkipping = false;
  private const float SkipTimeScale = 30f;

  [Header("ステップ7: 墓地システム")]
  public List<CemeteryRecord> cemeteryList = new List<CemeteryRecord>();
  private bool showCemeteryModal = false;

  // 手動合成 & 3択モーダル管理
  private bool showGrowthModal = false;
  private PieceData evolvingPiece = null;
  private List<GrowthType> growthOptions = new List<GrowthType>();

  // インスペクト（コマ選択）用
  public PieceData selectedPiece = null;
  private GameObject rangeIndicator = null;

  // バフマス管理
  private Vector3 buffTileLocalPos; // ステップ16: BoardTransform基準のローカル座標で保持
  public Vector3 buffTileGridPos => BoardTransform.TransformPoint(buffTileLocalPos); // 常に現在のワールド座標を返す
  private GameObject buffTileObject = null;

  private void Awake()
  {
    if (Instance == null) Instance = this;
    else Destroy(gameObject);
  }

  private void Start()
  {
    Time.timeScale = 1f; // ステップ6: 前回セッションの倍速設定を引き継がないよう保険でリセット

    // 課題1【SO一元化】: gameConfigが設定されている場合、初期Gold・初期HPはSO側の値を単一の真実として使用する。
    // 未設定の場合はInspector上のgold/playerHpフィールドの値がそのまま使われる（既存の挙動を維持）。
    if (gameConfig != null)
    {
      gold = gameConfig.initialGold;
      playerHp = gameConfig.initialPlayerHp;
    }

    GenerateBoardTiles();

    // 課題1: King配置座標も盤面サイズ(BoardWidth/BoardDepth)から算出する。
    // 既定の8x8設定であれば、従来通り (3,0) / (4,7) と同じ位置になる。
    SpawnKing(false, GridToWorldPosition(BoardWidth / 2 - 1, 0, 0.25f));
    SpawnKing(true, GridToWorldPosition(BoardWidth / 2, BoardDepth - 1, 0.25f));

    SetupWaveEnemies(currentWave);
    RerollShop(false);

    CreateRangeIndicator();
    GenerateBuffTile();
  }

  // ステップ15/16 → 今回改修: プレハブ方式でのチェス盤マス目生成。
  // 【新方式】tilePrefabが設定されている場合: 単一プレハブを共通で使い、
  //   (x+z)%2 の判定結果に応じて lightTileMaterial / darkTileMaterial をRendererへ適用してチェッカー模様にする。
  // 【旧方式・互換】tilePrefabが未設定の場合: 従来通りlightTilePrefab/darkTilePrefabの2種類のプレハブを出し分ける。
  void GenerateBoardTiles()
  {
    bool useSingleTileMode = tilePrefab != null;

    if (!useSingleTileMode && (lightTilePrefab == null || darkTilePrefab == null))
    {
      Debug.LogWarning("⚠️ 盤面生成: tilePrefab、または lightTilePrefab/darkTilePrefab のいずれかがInspectorで未設定です。マスは生成されません。");
      return;
    }

    // 課題1【中心合わせ】: 盤面サイズはgameConfig.boardWidth / boardDepthから決定する（未設定時は8x8）。
    // GetTileLocalPosition() が原点(0,0,0)を中心に対称な座標を返すため、
    // BoardWidth/BoardDepthをいくつに変更しても盤面全体は常にワールド原点付近を中心に生成される。
    for (int x = 0; x < BoardWidth; x++)
    {
      for (int z = 0; z < BoardDepth; z++)
      {
        // 課題1【チェッカー模様】: (x+z)が偶数なら明マス、奇数なら暗マスという市松模様の判定。
        // BoardWidth/BoardDepthのサイズが変わっても、この判定式だけで常に正しく交互配置される。
        bool isLight = (x + z) % 2 == 0;

        GameObject tile;
        GameObject sourcePrefab;
        if (useSingleTileMode)
        {
          // 【新方式】共通プレハブを1つ生成し、マテリアルだけを明/暗で出し分ける
          sourcePrefab = tilePrefab;
          tile = Instantiate(sourcePrefab);
          ApplyTileMaterial(tile, isLight ? lightTileMaterial : darkTileMaterial);
        }
        else
        {
          // 【旧方式・互換】明/暗それぞれ専用のプレハブをそのまま使う
          sourcePrefab = isLight ? lightTilePrefab : darkTilePrefab;
          tile = Instantiate(sourcePrefab);
        }

        // ステップ17【サイズ不整合の修正】: ワールド座標＋回転を同時指定するInstantiateはスケールの扱いが曖昧なため使わない。
        // 通常のInstantiate→SetParent(false)→localPositionのみ設定、という明示的な手順に統一する。
        // localScaleはプレハブが元々持っている値のまま一切上書きしないため、
        // 最終的な見た目のサイズは「プレハブ自身のスケール × BoardParentのlocalScale」だけで決まる。
        tile.transform.SetParent(BoardTransform, false);
        tile.transform.localPosition = GetTileLocalPosition(x, z);
        tile.transform.localRotation = sourcePrefab.transform.localRotation;
        tile.name = $"Tile_{x}_{z}_{(isLight ? "Light" : "Dark")}";
      }
    }

    // 【ベンチの切り分け】盤面グリッドとは完全に独立した専用メソッドでベンチを生成する
    GenerateBenchTiles();
  }

  // 課題1【ベンチハイライト】: 各ベンチスロットに対応するハイライト用オーバーレイ（初期は非表示）
  private List<GameObject> benchHighlightOverlays = new List<GameObject>();

  // 【ベンチの切り分け】盤面グリッド(BoardWidth/BoardDepth)の計算からは一切参照せず、
  // BenchSlotCount / BenchGapFromBoard という専用パラメータのみを使って、
  // 盤面の手前（Z軸マイナス側）に横一列で独立配置する。
  void GenerateBenchTiles()
  {
    GameObject prefabToUse = benchTilePrefab != null ? benchTilePrefab : tilePrefab;
    if (prefabToUse == null)
    {
      // ベンチ専用プレハブも共通tilePrefabも無い場合は、旧方式プレハブのうち明マスで代用する
      prefabToUse = lightTilePrefab;
    }
    if (prefabToUse == null)
    {
      Debug.LogWarning("⚠️ ベンチ生成: benchTilePrefab / tilePrefab / lightTilePrefab のいずれも未設定のため、ベンチは生成されません。");
      return;
    }

    benchHighlightOverlays.Clear();

    for (int i = 0; i < BenchSlotCount; i++)
    {
      GameObject tile = Instantiate(prefabToUse);
      if (benchTileMaterial != null) ApplyTileMaterial(tile, benchTileMaterial);

      Vector3 localPos = GetBenchLocalPosition(i);
      tile.transform.SetParent(BoardTransform, false);
      tile.transform.localPosition = localPos;
      tile.name = $"BenchSlot_{i}";

      // 課題1【ベンチハイライト修正】: 各スロットの真上にごく薄い光るオーバーレイ（Quad）を1枚生成しておき、
      // ドラッグ中に「空いているスロットだけ」SetActive(true)で点灯させる方式にする。
      // GenerateBuffTile()で使っているオーバーレイ表現と同じ手法（Sprites/Defaultシェーダー＋半透明色）に統一。
      GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
      glow.name = $"BenchSlot_{i}_Highlight";
      Destroy(glow.GetComponent<Collider>()); // ハイライト自体はクリック判定に影響させない

      glow.transform.SetParent(BoardTransform, false);
      Vector3 glowLocalPos = localPos;
      glowLocalPos.y = 0.02f;
      glow.transform.localPosition = glowLocalPos;
      glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      glow.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

      Renderer gr = glow.GetComponent<Renderer>();
      if (gr != null)
      {
        gr.material = new Material(Shader.Find("Sprites/Default"));
        gr.material.color = new Color(0.3f, 1.0f, 0.4f, 0.55f); // 空きスロットであることが分かりやすい黄緑系
      }

      glow.SetActive(false); // 初期状態は非表示。ドラッグ中のみ個別にON/OFFする
      benchHighlightOverlays.Add(glow);
    }
  }

  // 課題【3D/2D分離 & Indexベースのハイライト】
  // 「現在カーソルがドラッグ先として指しているベンチスロットのIndex」を、3D側・2D側の両方から
  // 参照できる単一の状態として保持する（-1 = どのスロットも対象になっていない）。
  // 実際の描画処理（3DはオーバーレイのSetActive、2DはUIManager側でのImage.color変更）は、
  // それぞれの側が「このIndexだけ」を見て個別に反映するため、3D/2Dの処理が混同されることはない。
  private int benchHoverIndex = -1;
  public int CurrentBenchHoverIndex => benchHoverIndex;

  // 3D側のドラッグ処理（PieceDraggable）から、フレームごとに「今カーソルが指しているベンチスロットのIndex」を
  // 通知してもらうためのAPI。既に駒が置かれているスロットは「有効なドロップ先ではない」ためハイライト対象から除外する。
  // 変化が無いフレームでは何もしない（Instantiate/SetActiveの呼び過ぎ防止）。
  public void SetBenchHoverIndex(int index)
  {
    if (index >= 0 && index < BenchSlotCount && UI_IsBenchSlotOccupied(index))
    {
      index = -1; // 既に駒があるスロットへは移動できないため、ハイライト対象にしない
    }

    if (benchHoverIndex == index) return;

    // 3D側: 直前にハイライトしていたスロットのオーバーレイだけをOFFにする
    if (benchHoverIndex >= 0 && benchHoverIndex < benchHighlightOverlays.Count && benchHighlightOverlays[benchHoverIndex] != null)
    {
      benchHighlightOverlays[benchHoverIndex].SetActive(false);
    }

    benchHoverIndex = index;

    // 3D側: 新しくホバー対象になったスロットのオーバーレイだけをONにする（他のスロットには一切触れない）
    if (benchHoverIndex >= 0 && benchHoverIndex < benchHighlightOverlays.Count && benchHighlightOverlays[benchHoverIndex] != null)
    {
      benchHighlightOverlays[benchHoverIndex].SetActive(true);
    }
  }

  // ドラッグ終了時: 3D側のハイライトを確実に消し、ホバー状態をリセットする
  public void ClearBenchHover()
  {
    if (benchHoverIndex >= 0 && benchHoverIndex < benchHighlightOverlays.Count && benchHighlightOverlays[benchHoverIndex] != null)
    {
      benchHighlightOverlays[benchHoverIndex].SetActive(false);
    }
    benchHoverIndex = -1;
  }

  // 生成したタイルのRenderer（複数階層に及ぶ場合も考慮しGetComponentInChildrenで探索）へマテリアルを適用する
  void ApplyTileMaterial(GameObject tile, Material material)
  {
    if (material == null) return;
    Renderer renderer = tile.GetComponentInChildren<Renderer>();
    if (renderer != null) renderer.sharedMaterial = material;
  }

  // =====================================================================
  // ステップ16: BoardParent基準の座標変換ユーティリティ
  // 盤面の位置・回転・スケールは全てBoardTransform（boardParent、未設定ならこのGameObject自身）
  // 一箇所に集約されており、マス・駒・バフマス・範囲リング等はすべてこの変換を経由することで、
  // BoardParentを動かす/拡大縮小するだけで全要素が自動的に連動する。
  // =====================================================================

  public Transform BoardTransform => boardParent != null ? boardParent : transform;
  // ステップ17: ワールド空間での距離判定（占有判定・AI移動幅）にのみ使う「現在の1マス分のワールドサイズ」。
  // 固定値ではなく、BoardTransformの実際のlocalScaleから毎回動的に算出するため、
  // 拡縮の設定箇所はBoardParentのlocalScaleただ1つに一本化される。
  public float WorldCellSize => BoardTransform.lossyScale.x;

  // 盤面グリッド(0〜BoardWidth-1, 0〜BoardDepth-1) → BoardTransformのローカル中心座標（1マス=ローカル1ユニット。倍率なし）
  // 課題1【中心合わせ】: X・Z両軸とも「(サイズ/2 - 0.5)」で対称にオフセットするため、
  // BoardWidth/BoardDepthの値に関わらず盤面全体は常にローカル原点（＝ワールド原点付近）を中心に生成される。
  public Vector3 GetTileLocalPosition(int gridX, int gridZ)
  {
    return new Vector3(gridX - (BoardWidth / 2f - 0.5f), 0f, gridZ - (BoardDepth / 2f - 0.5f));
  }

  // 【ベンチの切り分け】ベンチインデックス(0〜BenchSlotCount-1) → BoardTransformのローカル中心座標。
  // BoardWidth/BoardDepthを一切参照せず、BenchSlotCount/BenchGapFromBoardのみで独立して算出する。
  // X軸: ベンチ列自体もBenchSlotCountを基準に中心(x=0)を中心とした対称配置にする。
  // Z軸: 盤面の最前列（gridZ=0、ローカルZ = -(BoardDepth/2 - 0.5)）よりさらに手前（マイナス方向）に
  //      BenchGapFromBoard分の間隔を空けて配置する。
  public Vector3 GetBenchLocalPosition(int benchIndex)
  {
    float x = benchIndex - (BenchSlotCount / 2f - 0.5f);
    float boardFrontEdgeZ = -(BoardDepth / 2f - 0.5f);
    float z = boardFrontEdgeZ - BenchGapFromBoard - 0.5f;
    return new Vector3(x, 0f, z);
  }

  // 盤面グリッド座標 → ワールド座標（Y値は個別指定）
  public Vector3 GridToWorldPosition(int gridX, int gridZ, float worldY)
  {
    Vector3 world = BoardTransform.TransformPoint(GetTileLocalPosition(gridX, gridZ));
    world.y = worldY;
    return world;
  }

  // ベンチインデックス → ワールド座標
  public Vector3 BenchGridToWorldPosition(int benchIndex, float worldY)
  {
    Vector3 world = BoardTransform.TransformPoint(GetBenchLocalPosition(benchIndex));
    world.y = worldY;
    return world;
  }

  // ワールド座標 → BoardTransform基準のローカル座標（駒の現在位置がどのマスかを調べる時などに使用）
  public Vector3 WorldToBoardLocal(Vector3 worldPos)
  {
    return BoardTransform.InverseTransformPoint(worldPos);
  }

  // BoardTransform基準のローカル座標 → ワールド座標
  public Vector3 BoardLocalToWorld(Vector3 localPos)
  {
    return BoardTransform.TransformPoint(localPos);
  }

  // 【ベンチの切り分け】ワールド座標がベンチエリアかどうか。
  // 課題5【前列誤判定防止】: まず「盤面の範囲内（X/Z共に、Z=0の最前列を含む）」かどうかを判定し、
  // 盤面内であれば無条件で「ベンチではない」と確定させる（＝盤面内の駒がベンチと誤判定されて
  // 選択・ドラッグに支障が出ることが無いように、盤面内判定を最優先で行う）。
  // 盤面の範囲外にある場合のみ、さらに「ベンチ列の手前側エリアかどうか」を判定する。
  public bool IsWorldPositionInBenchArea(Vector3 worldPos)
  {
    Vector3 local = WorldToBoardLocal(worldPos);

    float boardMinX = -(BoardWidth / 2f - 0.5f) - 0.5f;
    float boardMaxX = (BoardWidth / 2f - 0.5f) + 0.5f;
    float boardMinZ = -(BoardDepth / 2f - 0.5f) - 0.5f;
    float boardMaxZ = (BoardDepth / 2f - 0.5f) + 0.5f;

    bool isWithinBoardExtent = local.x >= boardMinX && local.x <= boardMaxX && local.z >= boardMinZ && local.z <= boardMaxZ;
    if (isWithinBoardExtent) return false;

    float boardFrontEdgeZ = -(BoardDepth / 2f - 0.5f);
    float benchAreaThresholdZ = boardFrontEdgeZ - (BenchGapFromBoard / 2f);
    return local.z < benchAreaThresholdZ;
  }

  // ワールド座標を最も近い盤面グリッドインデックス(0〜BoardWidth-1, 0〜BoardDepth-1)に丸めて返す（範囲外の値はクランプしない生の値）
  public Vector2Int WorldToNearestGridIndex(Vector3 worldPos)
  {
    Vector3 local = WorldToBoardLocal(worldPos);
    int gridX = Mathf.RoundToInt(local.x + (BoardWidth / 2f - 0.5f));
    int gridZ = Mathf.RoundToInt(local.z + (BoardDepth / 2f - 0.5f));
    return new Vector2Int(gridX, gridZ);
  }

  // 【ベンチの切り分け】ワールド座標を最も近いベンチインデックス(0〜BenchSlotCount-1、クランプなしの生の値)に丸めて返す
  public int WorldToNearestBenchIndex(Vector3 worldPos)
  {
    Vector3 local = WorldToBoardLocal(worldPos);
    return Mathf.RoundToInt(local.x + (BenchSlotCount / 2f - 0.5f));
  }

  private void Update()
  {
    UpdateRangeIndicator();

    // ステップ6: キー入力はUpdate()で1フレーム1回だけ判定する（OnGUIは1フレームに複数回呼ばれるため）
    if (Keyboard.current != null)
    {
      if (Keyboard.current.f1Key.wasPressedThisFrame) showDebugMenu = !showDebugMenu;
      if (Keyboard.current.rKey.wasPressedThisFrame) ResetScene();
      if (Keyboard.current.spaceKey.wasPressedThisFrame && !isBattleStarted && !isGameOver) StartBattle();
    }

    // エンドレスモード移行告知バナーのカウントダウン（戦闘中/準備中を問わず動作）
    if (endlessAnnounceTimer > 0f)
    {
      endlessAnnounceTimer -= Time.deltaTime;
      if (endlessAnnounceTimer <= 0f) endlessAnnounceText = "";
    }

    if (!isBattleStarted || isGameOver) return;

    if (!isRoundEnding)
    {
      CheckBattleResult();
    }
    else
    {
      roundEndTimer += Time.deltaTime;
      if (roundEndTimer >= 2.0f)
      {
        AdvanceToNextWave();
      }
    }
  }

  void GenerateBuffTile()
  {
    if (buffTileObject != null) Destroy(buffTileObject);

    // 課題1: 元の抽選範囲（ワールドX: -2.5〜3.5 / ワールドZ: -1.5〜0.5、8x8想定でgridX 1〜7・gridZ 1〜3）を
    // BoardWidth/BoardDepthベースに一般化。既定の8x8設定であれば従来と全く同じ抽選範囲になる。
    int randGridX = Random.Range(1, BoardWidth);
    int randGridZ = Random.Range(1, Mathf.Max(2, BoardDepth / 2));

    Vector3 localPos = GetTileLocalPosition(randGridX, randGridZ);
    localPos.y = 0.01f;
    buffTileLocalPos = localPos;

    buffTileObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
    buffTileObject.name = "BuffTile";

    // ステップ16: BoardTransformの子・ローカル座標で配置し、BoardParentの変更に自動追従させる
    buffTileObject.transform.SetParent(BoardTransform, false);
    buffTileObject.transform.localPosition = buffTileLocalPos;
    buffTileObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    buffTileObject.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

    Destroy(buffTileObject.GetComponent<Collider>());

    Renderer ren = buffTileObject.GetComponent<Renderer>();
    if (ren != null)
    {
      ren.material = new Material(Shader.Find("Sprites/Default"));
      ren.material.color = new Color(1.0f, 0.85f, 0.2f, 0.6f);
    }
  }

  // 戦闘開始時のシナジー・キングスキル計算
  void ApplyFormationSynergies()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 以降のforeachループ内のロジック（isEnemy判定・ベンチ除外・currentHp>0の生存フィルタ等）は一切変更しない。
    IReadOnlyList<PieceData> allPieces = PieceRegistry.AllPieces;
    List<PieceData> playerPieces = new List<PieceData>();

    foreach (var p in allPieces)
    {
      if (p.isEnemy || p.currentHp <= 0 || IsWorldPositionInBenchArea(p.transform.position)) continue;
      playerPieces.Add(p);

      // キングスキル 1: 指揮のオーラ（全体攻撃バフ）
      if (skillAuraLevel > 0)
      {
        int perLevel = gameConfig != null ? gameConfig.skillAuraAttackPerLevel : 50;
        int auraBuff = skillAuraLevel * perLevel;
        p.attack += auraBuff;
        p.kingBonusAttack = auraBuff;
      }

      // キングスキル 3: 王の加護（耐久加算）
      if (skillBarrierLevel > 0)
      {
        int perLevel = gameConfig != null ? gameConfig.skillBarrierHpPerLevel : 300;
        int barrierHp = skillBarrierLevel * perLevel;
        p.maxHp += barrierHp;
        p.currentHp += barrierHp;
      }

      // バフマスチェック
      float dist = Vector2.Distance(new Vector2(p.transform.position.x, p.transform.position.z),
                                    new Vector2(buffTileGridPos.x, buffTileGridPos.z));
      if (dist < 0.4f)
      {
        p.isOnBuffTile = true;
        p.attack += 150;
        p.attackInterval *= 0.7f;
      }
    }

    // 隣接バフ計算
    foreach (var p in playerPieces)
    {
      foreach (var other in playerPieces)
      {
        if (p == other) continue;

        float gridDist = Vector3.Distance(p.transform.position, other.transform.position);

        if (gridDist > 0.8f && gridDist < 1.2f)
        {
          if (other.type == PieceType.Bishop)
          {
            p.maxHp += 30;
            p.currentHp += 30;
            p.hasAdjacentBuff = true;
          }
          if (other.type == PieceType.Rook)
          {
            p.attack += 10;
            p.hasAdjacentBuff = true;
          }
        }
      }
    }

    // ステップ5: エンドレスモードの敵無限強化
    // 敵駒はウェーブごとに新規スポーンされる（Kingを除く）ため、素のステータスに対して
    // 1回だけ倍率を適用すればよく、ウェーブを跨いだ二重加算にはならない。
    if (isEndlessMode)
    {
      float enemyMultiplier = Mathf.Pow(EndlessScalingRate, currentWave - (EndlessStartWave - 1));

      foreach (var p in allPieces)
      {
        if (!p.isEnemy || p.type == PieceType.King || p.currentHp <= 0) continue;

        int scaledMaxHp = Mathf.RoundToInt(p.maxHp * enemyMultiplier);
        p.maxHp = scaledMaxHp;
        p.currentHp = scaledMaxHp;
        p.attack = Mathf.RoundToInt(p.attack * enemyMultiplier);
      }
    }

    // 課題2【フロア選択の敵ステータス補正】: 3択フロア選択（弱敵/中敵/強敵）による敵ステータス倍率は、
    // 従来はここ（StartBattle時点）で初めて適用されていたが、それだと
    // 「フロア選択直後〜戦闘開始前」の間、内部データ(currentWaveEnemyMultiplier)は更新済みなのに
    // 実際の駒のステータス（延いては画面表示・実戦闘)には未反映という状態が発生してしまっていた。
    // 現在は UI_SelectWaveChoice() で選択された「その瞬間」に適用するよう変更したため、
    // ここでは何もしない（詳細は UI_SelectWaveChoice / ApplyWaveChoiceMultiplierToSpawnedEnemies を参照）。
  }

  public void SelectPiece(PieceData piece)
  {
    selectedPiece = piece;
  }

  void CreateRangeIndicator()
  {
    rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rangeIndicator.name = "RangeIndicator";
    Destroy(rangeIndicator.GetComponent<Collider>());

    Renderer ren = rangeIndicator.GetComponent<Renderer>();
    if (ren != null)
    {
      ren.material = new Material(Shader.Find("Sprites/Default"));
      ren.material.color = new Color(1.0f, 0.9f, 0.2f, 0.35f);
    }
    rangeIndicator.SetActive(false);
  }

  void UpdateRangeIndicator()
  {
    if (selectedPiece == null || !selectedPiece.gameObject.activeInHierarchy || selectedPiece.currentHp <= 0)
    {
      if (rangeIndicator != null) rangeIndicator.SetActive(false);
      return;
    }

    rangeIndicator.SetActive(true);
    rangeIndicator.transform.position = new Vector3(selectedPiece.transform.position.x, 0.05f, selectedPiece.transform.position.z);

    float attackRange = GetAttackRange(selectedPiece.type);
    float diameter = attackRange * 2.0f;
    rangeIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
  }

  float GetAttackRange(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    if (entry != null) return entry.attackRange;

    switch (type)
    {
      case PieceType.Queen: return 3.5f;
      case PieceType.Bishop: return 3.2f;
      case PieceType.Rook: return 1.8f;
      case PieceType.Knight: return 1.8f;
      case PieceType.Paladin: return 1.8f;
      case PieceType.King: return 1.8f;
      case PieceType.Pawn:
      default: return 1.8f;
    }
  }

  void CheckBattleResult()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    bool playerAlive = false;
    bool enemyAlive = false;
    int remainingEnemies = 0;

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.currentHp <= 0) continue;
      if (IsWorldPositionInBenchArea(p.transform.position)) continue;

      if (!p.isEnemy) playerAlive = true;
      else
      {
        enemyAlive = true;
        remainingEnemies++;
      }
    }

    if (!playerAlive || !enemyAlive)
    {
      isRoundEnding = true;
      roundEndTimer = 0f;

      if (!playerAlive && !enemyAlive)
      {
        gameResultText = "DRAW（引き分け）";
      }
      else if (!playerAlive)
      {
        // ステップ6: 敗北ダメージ = 生き残った敵の数 × 5
        int damage = remainingEnemies * 50;
        playerHp = Mathf.Max(0, playerHp - damage);
        gameResultText = $"LOSE... (-{damage} HP)";

        bool endlessTotalWipeout = isEndlessMode && !HasAnyAlivePlayerPiece() && !CanAffordCheapestShopPiece();

        if (playerHp <= 0 || endlessTotalWipeout)
        {
          isGameOver = true;
          gameResultText = endlessTotalWipeout ? "GAME OVER（戦力・資金ともに尽きました）" : "GAME OVER";

          // ステップ6: スキップ中にゲームオーバーへ到達した場合、AdvanceToNextWaveが呼ばれず
          // タイムスケールが高速のまま残ってしまうため、ここで明示的に復元する
          if (isSkipping)
          {
            Time.timeScale = speedOptions[currentSpeedIndex];
            isSkipping = false;
          }

          // ステップ7: 敗北してAdvanceToNextWaveが呼ばれないケースでも、
          // 最終ウェーブの戦死者を墓地へ記録し装備を清算するため、ここで明示的に呼ぶ
          CleanUpBattlefield();

          // ステップ5: 最終スコア計算＆ハイスコア保存
          finalScore = CalculateFinalScore();
          isNewHighScore = ScoreManager.SaveHighScoreIfBetter(finalScore, currentWave);
        }
      }
      else if (!enemyAlive)
      {
        // ステップ27【要件1】: 勝利報酬（SP）はエンドレスモード中は0加算（通常モードのみ+1SP）
        if (!isEndlessMode)
        {
          skillPoints += 1;
          gameResultText = "WIN！ (+1 SP 獲得)";
        }
        else
        {
          gameResultText = "WIN！";
        }
      }
    }
  }

  // ステップ27【要件3】: 盤上・ベンチを問わず、生存している味方駒が1体でもいるかどうか
  bool HasAnyAlivePlayerPiece()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (!p.isEnemy && p.currentHp > 0) return true;
    }
    return false;
  }

  // ステップ27【要件3】: 現在の所持Goldで、ショップで購入できる駒が1種類でもあるか（＝最安値と比較）
  bool CanAffordCheapestShopPiece()
  {
    int cheapest = int.MaxValue;
    foreach (PieceType type in System.Enum.GetValues(typeof(PieceType)))
    {
      if (type == PieceType.King || type == PieceType.Paladin) continue; // ショップ購入対象外の種別
      int cost = GetCost(type);
      if (cost < cheapest) cheapest = cost;
    }
    return gold >= cheapest;
  }

  void AdvanceToNextWave()
  {
    if (isGameOver) return;

    // ステップ6: スキップ中だった場合、ここで通常の速度設定へ戻す
    if (isSkipping)
    {
      Time.timeScale = speedOptions[currentSpeedIndex];
      isSkipping = false;
    }

    isBattleStarted = false;
    isRoundEnding = false;
    gameResultText = "";

    currentWave++;

    // ステップ5: Wave10クリア（=Wave11到達）でエンドレスモードへ移行
    if (currentWave == EndlessStartWave && !isEndlessMode)
    {
      isEndlessMode = true;
      endlessAnnounceText = "🌊 10 Wave クリア！ エンドレスモード開始！";
      endlessAnnounceTimer = 4f;
    }

    // キングスキル 2: 富の知識（追加ゴールド計算）※エンドレスモード中はゴールド獲得停止
    if (!isEndlessMode)
    {
      int baseGold = gameConfig != null ? gameConfig.waveBaseGoldReward : 500;
      int perLevel = gameConfig != null ? gameConfig.skillEconomyGoldPerLevel : 200;
      int bonusGold = baseGold + (skillEconomyLevel * perLevel);
      AddGold(bonusGold, GoldSourceType.WaveClear);
    }

    // ステップ4: フェーズ終了時に盤上の未回収ドロップを自動回収（エンドレス中はそもそも新規ドロップが発生しない）
    CollectAllRemainingDrops();

    // ステップ29【要件6】/課題2【エンドレスモードでの無効化】: 新しいウェーブの準備フェーズ開始にあたり、
    // 難易度倍率をリセットする。
    // 通常モード: 従来通り3択フロア選択モーダル（弱/中/強）を表示し、プレイヤーが選択する。
    // エンドレスモード: 3択UIは一切表示せず、代わりにApplyFormationSynergies()内の
    //   Mathf.Pow(EndlessScalingRate, ...) によるウェーブ数連動の自動強化のみが適用される
    //   （currentWaveEnemyMultiplierは常に1.0のまま＝3択由来の倍率は一切乗算しない）。
    currentWaveEnemyMultiplier = 1f;
    currentWaveDropRateMultiplier = 1f;
    waveChoiceStatApplied = false; // 課題2: 新しいウェーブが始まったので、倍率適用ガードもリセットする
    showWaveChoiceModal = !isEndlessMode;

    CleanUpBattlefield();
    SetupWaveEnemies(currentWave);
    RerollShop(false);
    GenerateBuffTile();
  }

  // =====================================================================
  // ステップ29【要件6】: 3択フロア選択システム（基礎設計）の公開API
  // =====================================================================

  // 課題1: gameConfig.waveChoiceOptionsが設定されていればそちらを単一の真実として使用し、
  // 未設定/空の場合はInspectorのwaveChoiceOptionsFallbackへフォールバックする。
  public WaveChoiceOption[] UI_GetWaveChoiceOptions()
  {
    if (gameConfig != null && gameConfig.waveChoiceOptions != null && gameConfig.waveChoiceOptions.Count > 0)
    {
      return gameConfig.waveChoiceOptions.ToArray();
    }
    return waveChoiceOptionsFallback;
  }

  public bool UI_IsWaveChoiceModalOpen() => showWaveChoiceModal;

  // ステップ30: UI側から参照するウェーブ数・リロールコストのゲッター
  public int UI_GetCurrentWave() => currentWave;
  public int UI_GetMaxNormalWave() => EndlessStartWave - 1; // 通常モード最終ウェーブ（=10）。Wave11以降はエンドレス
  public int UI_GetRerollCost() => RerollCost;

  // 選択肢を選ぶ: Gold即時付与＋以後の敵ステータス/ドロップ率倍率を確定させる
  public void UI_SelectWaveChoice(int index)
  {
    // 課題2【エンドレスモードでの無効化】: エンドレスモードでは3択UI自体を非表示にしているが、
    // 何らかの経路で誤って呼ばれた場合に備え、念のためここでも無視する防御的ガード
    if (isEndlessMode) return;

    WaveChoiceOption[] options = UI_GetWaveChoiceOptions();
    if (options == null || index < 0 || index >= options.Length) return;

    WaveChoiceOption option = options[index];
    currentWaveEnemyMultiplier = option.enemyStatMultiplier;
    currentWaveDropRateMultiplier = option.dropRateMultiplier;
    AddGold(option.goldReward, GoldSourceType.WaveChoiceReward);

    showWaveChoiceModal = false;

    // 課題2【修正】: 選択が確定した「その瞬間」に、既にスポーン済みの敵駒（King以外）の
    // 実ステータス（maxHp / currentHp / attack）へ直接倍率を適用する。
    // これにより、
    //   ・内部データ（currentWaveEnemyMultiplier）
    //   ・画面上のステータス表示（PieceInspectPanelUI等。piece.maxHp/attackを直接参照している）
    //   ・実際のバトル時の実効ステータス（PieceAIがmyData.attack等を直接参照して戦闘計算する）
    // の3つが、選択直後から常に一致した状態になる。
    // （従来はStartBattle()時点まで適用が遅延しており、フロア選択後〜戦闘開始前の
    //  ステータス確認画面だけ補正前の数値のままになってしまう不具合があった）
    ApplyWaveChoiceMultiplierToSpawnedEnemies();

    Debug.Log($"🚪【フロア選択】「{option.label}」を選択（敵倍率×{option.enemyStatMultiplier}, ドロップ率×{option.dropRateMultiplier}, +{option.goldReward}G）");
  }

  // 課題2: 現在盤上にいる敵駒（King以外・生存中）へcurrentWaveEnemyMultiplierを適用する。
  // waveChoiceStatAppliedガードにより、同一ウェーブ内で誤って複数回呼ばれても二重加算にはならない。
  void ApplyWaveChoiceMultiplierToSpawnedEnemies()
  {
    if (waveChoiceStatApplied) return;
    waveChoiceStatApplied = true;

    if (Mathf.Approximately(currentWaveEnemyMultiplier, 1f)) return;

    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (!p.isEnemy || p.type == PieceType.King || p.currentHp <= 0) continue;

      int scaledMaxHp = Mathf.RoundToInt(p.maxHp * currentWaveEnemyMultiplier);
      p.maxHp = scaledMaxHp;
      p.currentHp = scaledMaxHp;
      p.attack = Mathf.RoundToInt(p.attack * currentWaveEnemyMultiplier);

      // PieceHealthBarなど、直接値を参照しているUIがあれば同一フレームで追従できるよう明示的に更新を促す
      PieceHealthBar hpBar = p.GetComponent<PieceHealthBar>();
      if (hpBar != null) hpBar.UpdateHealthBar();
    }
  }

  void SetupWaveEnemies(int wave)
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 【副次効果】従来のFindObjectsOfType<PieceData>()（非アクティブ除外）では、撃破済み（SetActive(false)）の
    // 旧ウェーブの敵は見つからず、Destroy対象から漏れて非アクティブなまま残り続けていた。
    // PieceRegistry.AllPiecesは非アクティブな駒も含むため、このループで一緒にDestroyされるようになり、
    // 結果的に毎ウェーブの残骸掃除がより確実になる（このループはもともとcurrentHpによる生存フィルタが無く、
    // 「敵かつKing以外は無条件でDestroy」という意図の処理のため、ロジック自体は変更していない）。
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy && p.type != PieceType.King)
      {
        Destroy(p.gameObject);
      }
    }

    // ステップ24: EnemyWaveDataSOに該当Wave（1〜10想定）のデータがあればそちらを使用する。
    // 未設定、またはWave11以降（エンドレスモード）のようにデータが存在しない場合は
    // 既存のハードコード式ロジック（フォールバック）へ自動的に切り替わる。
    EnemyWaveDataSO.WaveEntry waveEntry = enemyWaveData != null ? enemyWaveData.GetWave(wave) : null;

    if (waveEntry != null)
    {
      foreach (var spawn in waveEntry.spawns)
      {
        // 課題【AIパターンのSO管理化】: このウェーブに設定されたAI行動パターンを、生成する敵全員へ伝播する
        SpawnEnemyAtGrid(spawn.type, spawn.gridX, spawn.gridZ, waveEntry.aiBehavior);
      }
      return;
    }

    // ---- フォールバック: 既存のハードコードロジック ----
    // 課題3: gridZの値はSpawnEnemyAtGrid内で盤面奥側2列（既定Z=6〜7）へ自動クランプされるが、
    // 意図と実際の見た目を一致させるため、値自体もあらかじめその範囲に収まるよう統一した。
    switch (wave)
    {
      case 1:
        SpawnEnemyAtGrid(PieceType.Pawn, 2, 6);
        SpawnEnemyAtGrid(PieceType.Pawn, 5, 6);
        break;
      case 2:
        SpawnEnemyAtGrid(PieceType.Pawn, 2, 6);
        SpawnEnemyAtGrid(PieceType.Pawn, 5, 6);
        SpawnEnemyAtGrid(PieceType.Knight, 4, 7);
        break;
      case 3:
        SpawnEnemyAtGrid(PieceType.Pawn, 1, 6);
        SpawnEnemyAtGrid(PieceType.Pawn, 6, 6);
        SpawnEnemyAtGrid(PieceType.Knight, 3, 7);
        SpawnEnemyAtGrid(PieceType.Bishop, 5, 7);
        break;
      default:
        SpawnEnemyAtGrid(PieceType.Rook, 2, 6);
        SpawnEnemyAtGrid(PieceType.Queen, 4, 6);
        SpawnEnemyAtGrid(PieceType.Knight, 5, 7);
        SpawnEnemyAtGrid(PieceType.Pawn, 3, 7);
        break;
    }
  }

  void CleanUpBattlefield()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>(true)（includeInactive:true）を
    // PieceRegistry.AllPiecesへ置き換え。PieceRegistry.AllPiecesは元々非アクティブな
    // （撃破済みでSetActive(false)された）駒も含んでいるため、includeInactive:true相当の指定は不要になる
    // （ここは元々「非アクティブな駒も意図的に含めて処理する」唯一の特別な箇所だった）。
    foreach (var p in PieceRegistry.AllPieces)
    {
      // ステップ27【要件2】: キングは死亡していても毎ウェーブ必ず全回復して復活させる。
      // 通常の生死判定より先に処理し、以降の分岐はスキップする。
      if (p.type == PieceType.King)
      {
        p.currentHp = p.maxHp;

        // savedPositionはプレイヤー側駒のみ記録されるため、キングはスポーン地点（自陣/敵陣）へ固定復帰させる
        Vector3 homePos = p.isEnemy
          ? GridToWorldPosition(BoardWidth / 2, BoardDepth - 1, 0.25f)
          : GridToWorldPosition(BoardWidth / 2 - 1, 0, 0.25f);
        p.transform.position = homePos;

        if (!p.gameObject.activeInHierarchy) p.gameObject.SetActive(true);

        p.isOnBuffTile = false;
        p.hasAdjacentBuff = false;
        p.kingBonusAttack = 0;
        p.hasRebirthedThisBattle = false; // 課題1: 次のバトルでもまたリバース判定できるようリセット

        // 課題5【HPバー非表示バグ対策】: キングは死亡していてもここで毎ウェーブ強制復活するため、
        // HP変更直後に明示的にHPバー表示を更新する（PieceHealthBar側でも常時保険をかけてあるが、二重の安全策）
        PieceHealthBar kingHpBar = p.GetComponent<PieceHealthBar>();
        if (kingHpBar != null) kingHpBar.UpdateHealthBar();

        continue;
      }

      if (p.currentHp <= 0)
      {
        if (!p.isEnemy)
        {
          // ステップ27【要件4】: 敗北ペナルティ緩和。一律消失ではなく、
          // 一定確率で本当に死亡（墓地送り・装備清算・破棄）、それ以外は
          // 瀕死のまま生存（HP1・装備そのまま・元の位置へワープ）させる。
          // 課題1/3【SO一元化】: 生存確率をハードコード(0.3f)からGameConfigSO.playerNearDeathSurviveChanceへ切り出した。
          float surviveChance = gameConfig != null ? gameConfig.playerNearDeathSurviveChance : 0.7f;
          bool trueDeath = Random.value >= surviveChance;

          if (trueDeath)
          {
            SendPieceToCemetery(p);
            Destroy(p.gameObject);
          }
          else
          {
            p.currentHp = 1;
            if (!p.gameObject.activeInHierarchy) p.gameObject.SetActive(true);
            p.transform.position = p.savedPosition;
            p.isOnBuffTile = false;
            p.hasAdjacentBuff = false;
            p.kingBonusAttack = 0;
            p.hasRebirthedThisBattle = false; // 課題1: 次のバトルでもまたリバース判定できるようリセット

            // 課題5【HPバー非表示バグ対策】: 瀕死生存もHPが0→1へ変化する「復活」の一種のため、
            // 明示的にHPバー表示を更新する
            PieceHealthBar survivorHpBar = p.GetComponent<PieceHealthBar>();
            if (survivorHpBar != null) survivorHpBar.UpdateHealthBar();

            Debug.Log($"🩹 {p.pieceName} は瀕死ながら生き残った！（HP1）");
          }
        }
        else
        {
          Destroy(p.gameObject);
        }
      }
      else if (!p.isEnemy)
      {
        // ステップ6: 戦闘開始時に記憶した座標へワープして元の配置へ戻す
        p.transform.position = p.savedPosition;

        // ステップ7【難易度強化】: 自動全回復を廃止。現在のHPをそのまま次のウェーブへ引き継ぐ。
        // 回復手段はビショップの回復スキル・吸血・スキルツリー・Gold治療（ステップ27）等に限定される。
        p.currentHp = Mathf.Clamp(p.currentHp, 0, p.maxHp);

        p.isOnBuffTile = false;
        p.hasAdjacentBuff = false;
        p.kingBonusAttack = 0;
      }
    }
  }

  [Tooltip("gameConfigが未設定の場合に使う、負傷した味方駒をGoldで全回復させる際のフォールバックコスト。\nUI側は必ずUI_GetHealCost()経由でこの値を参照すること（値の二重管理を防止するための単一の真実）")]
  [SerializeField] private int healCostFallback = 2000;

  private int HealCost => gameConfig != null ? gameConfig.healCost : healCostFallback;

  public int UI_GetHealCost() => HealCost;

  // ステップ29【要件2】/ステップ31【改善】: 負傷した味方駒をGoldで全回復させる。
  // コストは引数で受け取らず、必ず上記のHealCostプロパティ（単一の真実）を参照する。
  public bool HealPieceWithGold(PieceData piece)
  {
    if (piece == null) return false;
    if (piece.isEnemy) return false;
    if (piece.currentHp <= 0) return false; // 死亡済み（本当に消失した）駒は対象外
    if (piece.currentHp >= piece.maxHp) return false; // 満タンなら不要

    if (gold < HealCost)
    {
      Debug.LogWarning($"⚠️ Goldが足りません（必要: {HealCost}G / 所持: {gold}G）。");
      return false;
    }

    AddGold(-HealCost, GoldSourceType.ManualHeal);
    piece.Heal(piece.maxHp - piece.currentHp); // 既存のHeal()を再利用し、浮遊テキスト等の演出もそのまま活かす
    return true;
  }

  // UI（PieceInspectPanelUI等）からの呼び出し用エイリアス。既存の命名規則(UI_〜)との互換のために残す
  public bool UI_HealPieceWithGold(PieceData piece) => HealPieceWithGold(piece);

  // ステップ7: 戦死した味方駒を墓地リストへ記録し、装備を1つずつ50%抽選で回収 or ロスト
  void SendPieceToCemetery(PieceData piece)
  {
    CemeteryRecord record = new CemeteryRecord
    {
      pieceName = piece.pieceName,
      type = piece.type,
      rank = piece.rank,
      deathWave = currentWave
    };

    int recovered = 0;
    int lost = 0;

    if (piece.equippedItems.Count > 0)
    {
      List<EquipmentInstance> items = new List<EquipmentInstance>(piece.equippedItems);

      foreach (var item in items)
      {
        bool isRecovered = Random.value < 0.5f;

        if (isRecovered)
        {
          AddItemToInventory(item);
          recovered++;
        }
        else
        {
          lost++;
        }

        // ステップ8: 墓地データに装備1個ごとの結末（名前・レアリティ・回収 or ロスト）を記録
        record.equipmentLog.Add(new CemeteryEquipmentEntry
        {
          itemName = item.itemName,
          rarity = item.rarity,
          wasRecovered = isRecovered
        });
      }
    }

    cemeteryList.Add(record);

    Debug.Log($"【墓地】{piece.pieceName} が戦死。装備 {recovered}個 回収 / {lost}個 ロスト。");
  }

  private void OnGUI()
  {
    // ステップ11: エンドレスモード移行告知バナー（一時演出のためOnGUIのまま維持）
    if (endlessAnnounceText != "")
    {
      GUIStyle endlessStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
      endlessStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
      GUI.Label(new Rect(Screen.width / 2 - 300, 120, 600, 60), endlessAnnounceText, endlessStyle);
    }

    if (gameResultText != "" && !isGameOver)
    {
      GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
      style.normal.textColor = gameResultText.Contains("WIN") ? Color.green : Color.red;
      GUI.Label(new Rect(Screen.width / 2 - 250, Screen.height / 2 - 50, 500, 100), gameResultText, style);
    }

    // ステップ23: 合成ボタン・強化選択モーダルはUGUI（MergeButtonsUI / GrowthModalUI）へ移行したため、
    // 旧OnGUI版のCheckAndDrawMergeButtons / DrawGrowthModalの呼び出しは削除しました。

    // ステップ5: ゲームオーバー時のスコア画面
    if (isGameOver) DrawGameOverPanel();

    // ステップ6: デバッグ専用UI（F1キー or UGUIのDebugトグルボタンで表示切替）
    if (showDebugMenu) DrawDebugMenu();
  }

  // ステップ11: メインHUD・ショップ・ベンチはUIManager（UGUI）へ移行したため、
  // 旧OnGUI版のDrawMainHud / DrawShopAndBenchUI / CountOccupiedBenchSlotsは削除しました。

  // ステップ6: デバッグ専用UI（開発者向けの手動操作。showDebugMenuで表示切替）
  void DrawDebugMenu()
  {
    GUI.backgroundColor = Color.cyan;
    if (GUI.Button(new Rect(20, 20, 140, 35), "🔄 リセット (R)")) ResetScene();

    if (!isBattleStarted && !isGameOver)
    {
      GUI.backgroundColor = Color.yellow;
      int debugGoldAmount = gameConfig != null ? gameConfig.debugGoldGrantAmount : 10;
      if (GUI.Button(new Rect(140, 107, 80, 26), $"+{debugGoldAmount} Gold")) AddGold(debugGoldAmount, GoldSourceType.Debug);

      // 開発用召喚UI
      GUI.backgroundColor = Color.gray;
      GUI.Label(new Rect(20, Screen.height - 210, 140, 20), "【開発用召喚】");

      if (GUI.Button(new Rect(20, Screen.height - 185, 110, 26), "🏰 ルーク (1)")) SpawnPieceToBenchOrBoard(PieceType.Rook, false, GetPieceColor(PieceType.Rook));
      if (GUI.Button(new Rect(20, Screen.height - 155, 110, 26), "🪄 ビショップ(2)")) SpawnPieceToBenchOrBoard(PieceType.Bishop, false, GetPieceColor(PieceType.Bishop));
      if (GUI.Button(new Rect(20, Screen.height - 125, 110, 26), "♟️ ポーン (3)")) SpawnPieceToBenchOrBoard(PieceType.Pawn, false, GetPieceColor(PieceType.Pawn));
      if (GUI.Button(new Rect(20, Screen.height - 95, 110, 26), "♞ ナイト (4)")) SpawnPieceToBenchOrBoard(PieceType.Knight, false, GetPieceColor(PieceType.Knight));
      if (GUI.Button(new Rect(20, Screen.height - 65, 110, 26), "👑 クイーン (5)")) SpawnPieceToBenchOrBoard(PieceType.Queen, false, GetPieceColor(PieceType.Queen));
    }

    // 敵手動追加UI
    GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
    GUI.Label(new Rect(Screen.width - 140, 20, 120, 20), "【敵の手動追加】");
    if (GUI.Button(new Rect(Screen.width - 140, 45, 120, 28), "+ 敵ポーン")) SpawnPiece(PieceType.Pawn, true, Color.red);
    if (GUI.Button(new Rect(Screen.width - 140, 78, 120, 28), "+ 敵ナイト")) SpawnPiece(PieceType.Knight, true, new Color(1.0f, 0.4f, 0.2f));
    if (GUI.Button(new Rect(Screen.width - 140, 111, 120, 28), "+ 敵ルーク")) SpawnPiece(PieceType.Rook, true, new Color(0.8f, 0.2f, 0.2f));
    if (GUI.Button(new Rect(Screen.width - 140, 144, 120, 28), "+ 敵ビショップ")) SpawnPiece(PieceType.Bishop, true, new Color(0.9f, 0.3f, 0.5f));
    if (GUI.Button(new Rect(Screen.width - 140, 177, 120, 28), "+ 敵クイーン")) SpawnPiece(PieceType.Queen, true, new Color(0.6f, 0.0f, 0.4f));

    // ハイスコアリセット（デバッグ用）
    GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
    if (GUI.Button(new Rect(Screen.width - 170, Screen.height - 80, 150, 30), "🗑 ハイスコアリセット"))
    {
      ScoreManager.ResetHighScore();
    }
  }

  int CalculateFinalScore()
  {
    int waveScore = currentWave * ScorePerWave;
    int killScore = totalEnemiesDefeated * ScorePerKill;
    int bonusScore = (gold * ScorePerGold) + (playerHp * ScorePerHp);
    return waveScore + killScore + bonusScore;
  }

  void DrawGameOverPanel()
  {
    int width = 480;
    int height = 320;
    int startX = Screen.width / 2 - width / 2;
    int startY = Screen.height / 2 - height / 2;

    GUI.Box(new Rect(startX, startY, width, height), "");

    GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    titleStyle.normal.textColor = Color.red;
    GUI.Label(new Rect(startX, startY + 15, width, 40), "GAME OVER", titleStyle);

    GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
    infoStyle.normal.textColor = Color.white;

    string waveText = isEndlessMode ? $"🌊 到達ウェーブ: {currentWave} (ENDLESS)" : $"到達ウェーブ: {currentWave}";
    GUI.Label(new Rect(startX, startY + 65, width, 26), waveText, infoStyle);
    GUI.Label(new Rect(startX, startY + 91, width, 26), $"総撃破数: {totalEnemiesDefeated}", infoStyle);
    GUI.Label(new Rect(startX, startY + 117, width, 26), $"最終ゴールド: {gold}   残HP: {playerHp}", infoStyle);

    GUIStyle scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    scoreStyle.normal.textColor = Color.yellow;
    GUI.Label(new Rect(startX, startY + 152, width, 36), $"⭐ 最終スコア: {finalScore} pt", scoreStyle);

    if (isNewHighScore)
    {
      GUIStyle recordStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
      recordStyle.normal.textColor = new Color(1f, 0.4f, 0.8f);
      GUI.Label(new Rect(startX, startY + 192, width, 28), "New Record!", recordStyle);
    }
    else
    {
      GUIStyle hsStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
      hsStyle.normal.textColor = Color.gray;
      GUI.Label(new Rect(startX, startY + 192, width, 28),
        $"🏆 ハイスコア: {ScoreManager.GetHighScore()} pt (Wave {ScoreManager.GetHighScoreWave()})", hsStyle);
    }

    GUI.backgroundColor = Color.cyan;
    if (GUI.Button(new Rect(startX + width / 2 - 90, startY + height - 55, 180, 36), "🔄 再挑戦 (R)"))
    {
      ResetScene();
    }
  }

  // ステップ13: スキルツリー/墓地モーダルはUIManager（Editor上のCanvas Prefab + Inspector参照）へ移行したため、
  // 旧OnGUI版のDrawSkillTreeModal / DrawCemeteryModalは削除しました。

  // ステップ19: 駒のステータスパネルはUGUI（PieceInspectPanelUI.cs）へ移行したため、
  // 旧OnGUI版のDrawPieceInspectPanelは削除しました。

  // =====================================================================
  // 課題【合成/融合の手動選択モード】: 合成/融合ボタンを押すと対象の駒が自動選択されて即座に実行される
  // 従来の方式から、「対象条件に合う駒の中からプレイヤー自身が誰を使うか選び、確定ボタンを押すまで
  // 実行されない」という2段階の操作へ変更するための状態管理。
  // 盤面上の駒への直接クリック（PieceDraggable経由）と、一覧リストUI（MergeSelectionListUI経由）の
  // どちらからでも同じ選択状態（selectedPieces）を共有・操作できる。
  // =====================================================================

  public struct SelectionRequirement
  {
    public PieceType type;
    public int fromRank;
    public int requiredCount;
  }

  private bool isSelectionModeActive = false;
  private List<SelectionRequirement> selectionRequirements = new List<SelectionRequirement>();
  private List<PieceData> selectedPieces = new List<PieceData>();
  // fromRank==1または2の場合はExecuteMerge/ExecuteStar3Evolution、
  // fusionRecipeIndex>=0の場合はExecuteFusionCore相当の処理を確定時に呼び分けるために保持する
  private int selectionFromRank = 0;
  private int selectionFusionRecipeIndex = -1;

  // 課題【★2→★3合成の育成履歴分岐システム】: 合成候補1件分の情報（駒種・元のrank・体数）
  public struct MergeCandidateInfo
  {
    public PieceType type;
    public int fromRank;
    public int count;
  }

  // ステップ23: UGUI（MergeButtonsUI）から参照する合成候補一覧（★1が3体以上そろっている駒種と数、
  // および課題【★2→★3合成】で追加した★2が3体以上そろっている対象4駒種と数）
  public List<MergeCandidateInfo> UI_GetMergeCandidates()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 【要フィルタ追加】従来のFindObjectsOfType<PieceData>()は非アクティブ（撃破済み）の駒を自動的に除外していたが、
    // PieceRegistry.AllPiecesは非アクティブな駒も含むため、挙動を変えないよう currentHp <= 0 の除外を明示的に追加した。
    Dictionary<PieceType, int> rank1Counts = new Dictionary<PieceType, int>();
    Dictionary<PieceType, int> rank2Counts = new Dictionary<PieceType, int>();

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy || p.currentHp <= 0) continue;

      // rank1の集計: 既存のロジック（p.isEnemy || p.type==King || p.rank!=1 を除外）をそのまま踏襲
      if (p.type != PieceType.King && p.rank == 1)
      {
        if (!rank1Counts.ContainsKey(p.type)) rank1Counts[p.type] = 0;
        rank1Counts[p.type]++;
      }
      // 課題【★2→★3合成】: rank2の集計は対象4駒種（ナイト/ルーク/ビショップ/クイーン）のみに絞る
      else if (p.rank == 2 && IsStar3EvolutionEligible(p.type))
      {
        if (!rank2Counts.ContainsKey(p.type)) rank2Counts[p.type] = 0;
        rank2Counts[p.type]++;
      }
    }

    List<MergeCandidateInfo> result = new List<MergeCandidateInfo>();
    foreach (var kv in rank1Counts)
    {
      if (kv.Value >= 3) result.Add(new MergeCandidateInfo { type = kv.Key, fromRank = 1, count = kv.Value });
    }
    foreach (var kv in rank2Counts)
    {
      if (kv.Value >= 3) result.Add(new MergeCandidateInfo { type = kv.Key, fromRank = 2, count = kv.Value });
    }
    return result;
  }

  // 課題【★2→★3合成の育成履歴分岐システム】: ★2→★3合成の対象となる駒種かどうか。
  // ポーンは既存のEvolveToPaladin()による専用進化を維持するため対象外、キング/パラディンは
  // UI_GetMergeCandidatesの対象外のまま（King/Paladinはrank2集計にすら乗らない）。
  bool IsStar3EvolutionEligible(PieceType type)
  {
    return type == PieceType.Knight || type == PieceType.Rook || type == PieceType.Bishop || type == PieceType.Queen;
  }

  // ステップ23: UGUIの合成ボタンから呼ばれる。指定した駒種の★{fromRank}を3体探して合成/進化を実行する
  public void UI_ExecuteMerge(PieceType type, int fromRank)
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 【要フィルタ追加】上と同じ理由で、currentHp <= 0 の除外を明示的に追加した。
    List<PieceData> targets = new List<PieceData>();

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy || p.type != type || p.rank != fromRank || p.currentHp <= 0) continue;
      targets.Add(p);
      if (targets.Count == 3) break;
    }

    if (targets.Count < 3)
    {
      Debug.LogWarning($"⚠️ {type} の★{fromRank}が3体そろっていないため合成できません。");
      return;
    }

    // 課題【★2→★3合成の育成履歴分岐システム】: fromRankに応じて既存の★1→★2合成と
    // 新規の★2→★3進化のどちらを実行するか分岐する。
    if (fromRank == 1)
    {
      ExecuteMerge(targets);
    }
    else if (fromRank == 2)
    {
      ExecuteStar3Evolution(targets);
    }
    else
    {
      Debug.LogWarning($"⚠️ UI_ExecuteMerge: 未対応のfromRank({fromRank})が指定されました。");
    }
  }

  void ExecuteMerge(List<PieceData> targets)
  {
    PieceData keepPiece = targets[0];
    PieceData remove1 = targets[1];
    PieceData remove2 = targets[2];

    // ステップ4: 装備保護。合成される3体全ての装備を強制的に外し、あとでインベントリへ戻す
    List<EquipmentInstance> protectedItems = new List<EquipmentInstance>();
    protectedItems.AddRange(keepPiece.UnequipAll());
    protectedItems.AddRange(remove1.UnequipAll());
    protectedItems.AddRange(remove2.UnequipAll());

    Destroy(remove1.gameObject);
    Destroy(remove2.gameObject);

    keepPiece.EvolveToStar2();
    evolvingPiece = keepPiece;

    foreach (var item in protectedItems)
    {
      AddItemToInventory(item);
    }

    GenerateGrowthOptions();
    showGrowthModal = true;
  }

  // 課題【★2→★3合成の育成履歴分岐システム】: ★2×3体を合成し、それぞれのgrowthHistoryの傾向に応じた
  // 派生駒（フレーバー名+追加ステータス）へ進化させる。★1→★2のExecuteMergeとは異なり、
  // プレイヤーによる選択モーダルは使用しない（3体の履歴で自動的に結果が決まるため、showGrowthModalは使わない）。
  void ExecuteStar3Evolution(List<PieceData> targets)
  {
    PieceData keepPiece = targets[0];
    PieceData remove1 = targets[1];
    PieceData remove2 = targets[2];

    // 進化分岐の判定は、remove1/remove2がDestroyされる前に済ませておく必要がある
    EvolutionVariant variant = DetermineEvolutionVariant(targets);

    // ステップ4と同じ装備保護の流れ: 合成される3体全ての装備を強制的に外し、あとでインベントリへ戻す
    List<EquipmentInstance> protectedItems = new List<EquipmentInstance>();
    protectedItems.AddRange(keepPiece.UnequipAll());
    protectedItems.AddRange(remove1.UnequipAll());
    protectedItems.AddRange(remove2.UnequipAll());

    Destroy(remove1.gameObject);
    Destroy(remove2.gameObject);

    // まず既存の通常成長（HP×1.8, ATK×1.5等）を適用する
    // （RankUp()内のPawn専用分岐は今回対象外の駒種（Knight/Rook/Bishop/Queen）のみを扱うため実質素通りする）
    keepPiece.RankUp();

    EvolutionRuleDataSO.EvolutionRuleEntry rule = evolutionRuleData != null ? evolutionRuleData.GetRule(keepPiece.type, variant) : null;

    if (rule != null)
    {
      if (rule.attackBonusMultiplier != 0f)
      {
        keepPiece.attack = Mathf.RoundToInt(keepPiece.attack * (1f + rule.attackBonusMultiplier));
      }
      if (rule.hpBonusMultiplier != 0f)
      {
        int newMaxHp = Mathf.RoundToInt(keepPiece.maxHp * (1f + rule.hpBonusMultiplier));
        keepPiece.maxHp = newMaxHp;
        keepPiece.currentHp = newMaxHp;
      }
      if (rule.speedBonusRate != 0f)
      {
        keepPiece.attackInterval *= (1f - rule.speedBonusRate);
        keepPiece.attackInterval = Mathf.Max(keepPiece.attackInterval, 0.3f); // PieceData.ApplyEquipmentBonus等と同じ下限ガードを踏襲
      }
      if (rule.lifestealBonusRate != 0f)
      {
        keepPiece.lifestealRate += rule.lifestealBonusRate;
      }
      if (rule.grantsExtraEquipSlot)
      {
        keepPiece.bonusEquipSlots += 1;
      }

      keepPiece.evolvedVariantName = rule.evolvedName;
      keepPiece.evolvedVariantDescription = rule.evolvedDescription;
    }
    else
    {
      keepPiece.evolvedVariantName = "";
      keepPiece.evolvedVariantDescription = "";
    }

    foreach (var item in protectedItems)
    {
      AddItemToInventory(item);
    }

    PieceHealthBar hpBar = keepPiece.GetComponent<PieceHealthBar>();
    if (hpBar != null) hpBar.UpdateHealthBar();

    // 既存のShowGrowthPopup相当の演出。進化結果を画面上に表示する
    Vector3 popupPos = keepPiece.transform.position + Vector3.up * 1.1f;
    string popupText = !string.IsNullOrEmpty(keepPiece.evolvedVariantName)
      ? $"{keepPiece.evolvedVariantName} に進化！"
      : $"★3 {keepPiece.pieceName} に進化！";
    DamagePopup.Create(popupPos, popupText, DamagePopupType.Critical);

    Debug.Log($"✨【★3進化】{keepPiece.pieceName} が『{(!string.IsNullOrEmpty(keepPiece.evolvedVariantName) ? keepPiece.evolvedVariantName : "無銘")}』に進化しました！（傾向: {variant}）");
  }

  // 課題【★2→★3合成の育成履歴分岐システム】: 3体それぞれのgrowthHistory（各1件ずつ入っている想定）を集計し、
  // 2体以上が同じGrowthTypeを選んでいればそれを主属性としてEvolutionVariantへマッピングする。該当なしならBalanced。
  // 防御的措置: あるpieceのgrowthHistoryが空だった場合はその1票をカウントに含めない。
  EvolutionVariant DetermineEvolutionVariant(List<PieceData> targets)
  {
    Dictionary<GrowthType, int> voteCounts = new Dictionary<GrowthType, int>();

    foreach (var p in targets)
    {
      if (p.growthHistory == null || p.growthHistory.Count == 0) continue;

      // 各駒は★2進化時に1件だけ記録される想定だが、念のため最新（末尾）の1件を投票として扱う
      GrowthType vote = p.growthHistory[p.growthHistory.Count - 1];
      if (!voteCounts.ContainsKey(vote)) voteCounts[vote] = 0;
      voteCounts[vote]++;
    }

    foreach (var kv in voteCounts)
    {
      if (kv.Value >= 2) return MapGrowthTypeToVariant(kv.Key);
    }

    return EvolutionVariant.Balanced;
  }

  EvolutionVariant MapGrowthTypeToVariant(GrowthType type)
  {
    switch (type)
    {
      case GrowthType.AttackUp: return EvolutionVariant.AttackFocused;
      case GrowthType.HpUp: return EvolutionVariant.HpFocused;
      case GrowthType.SpeedUp: return EvolutionVariant.SpeedFocused;
      case GrowthType.Lifesteal: return EvolutionVariant.LifestealFocused;
      default: return EvolutionVariant.Balanced;
    }
  }

  // =====================================================================
  // 課題【異種合成「精鋭騎兵」】: ★1×3→★2の同種合成（ExecuteMerge/UI_GetMergeCandidates/UI_ExecuteMerge）や
  // ★2×3→★3の育成履歴分岐（ExecuteStar3Evolution）とは別系統の、異なる駒種同士を消費する融合システム。
  // 既存の同種合成ロジックには一切手を加えていない。
  // =====================================================================

  // 融合候補1件分の情報（レシピIndex・表示名・必要素材が揃っているか）
  public struct FusionCandidateInfo
  {
    public int recipeIndex;
    public string recipeName;
    public bool isAvailable; // 必要素材が両方揃っているか
  }

  // FusionButtonsUIから参照する融合候補一覧。fusionRecipeData未設定の場合は空リストを返す。
  public List<FusionCandidateInfo> UI_GetFusionCandidates()
  {
    List<FusionCandidateInfo> result = new List<FusionCandidateInfo>();
    if (fusionRecipeData == null) return result;

    for (int i = 0; i < fusionRecipeData.recipes.Count; i++)
    {
      FusionRecipeEntry recipe = fusionRecipeData.recipes[i];

      // 課題【異種合成】: materialType1とmaterialType2が同じ場合（今回は無いが将来のため）でも、
      // 合算せず種類ごとに個別カウントする（要件通り）。
      int count1 = CountAvailableMaterial(recipe.materialType1);
      int count2 = CountAvailableMaterial(recipe.materialType2);

      bool isAvailable = count1 >= recipe.materialCount1 && count2 >= recipe.materialCount2;

      result.Add(new FusionCandidateInfo
      {
        recipeIndex = i,
        recipeName = recipe.recipeName,
        isAvailable = isAvailable
      });
    }

    return result;
  }

  // 課題【異種合成】: 指定した駒種の「素材として使える自駒（★1・生存中）」の数を数える
  int CountAvailableMaterial(PieceType type)
  {
    int count = 0;
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy || p.currentHp <= 0 || p.rank != 1 || p.type != type) continue;
      count++;
    }
    return count;
  }

  // FusionButtonUIのクリックから呼ばれる。指定したレシピの素材を集めて融合を実行する
  public void UI_ExecuteFusion(int recipeIndex)
  {
    if (fusionRecipeData == null) return;
    if (recipeIndex < 0 || recipeIndex >= fusionRecipeData.recipes.Count) return;

    FusionRecipeEntry recipe = fusionRecipeData.recipes[recipeIndex];

    List<PieceData> materials1 = CollectMaterial(recipe.materialType1, recipe.materialCount1);
    List<PieceData> materials2 = CollectMaterial(recipe.materialType2, recipe.materialCount2);

    if (materials1.Count < recipe.materialCount1 || materials2.Count < recipe.materialCount2)
    {
      Debug.LogWarning($"⚠️ 「{recipe.recipeName}」に必要な素材（{recipe.materialType1}×{recipe.materialCount1} / {recipe.materialType2}×{recipe.materialCount2}）が揃っていません。");
      return;
    }

    ExecuteFusionCore(recipe, materials1, materials2);
  }

  // 課題【合成/融合の手動選択モード】: UI_ExecuteFusion()の実行本体をここへ切り出した。
  // 自動収集（CollectMaterial、UI_ExecuteFusion経由）・手動選択確定（UI_ConfirmSelection経由）の
  // どちらから呼ばれても、以降の処理（装備保護→Destroy→スポーン→演出）は完全に共通のロジックを通る。
  void ExecuteFusionCore(FusionRecipeEntry recipe, List<PieceData> materials1, List<PieceData> materials2)
  {
    // 集めた駒のうち1体（materialType1側の1体目）の位置を融合後の駒のスポーン地点とする
    Vector3 spawnPos = materials1[0].transform.position;

    // 既存のExecuteMerge/ExecuteStar3Evolutionと同じ装備保護の流れ:
    // 消費される全ての駒の装備を強制的に外し、あとでインベントリへ戻す
    List<EquipmentInstance> protectedItems = new List<EquipmentInstance>();
    foreach (var p in materials1) protectedItems.AddRange(p.UnequipAll());
    foreach (var p in materials2) protectedItems.AddRange(p.UnequipAll());

    foreach (var p in materials1) Destroy(p.gameObject);
    foreach (var p in materials2) Destroy(p.gameObject);

    foreach (var item in protectedItems)
    {
      AddItemToInventory(item);
    }

    // SpawnPieceAt内部でSetupInitialStats()が呼ばれ、UnitStatusDataSOのresultType用エントリから自動的に初期化される
    Color resultColor = GetPieceColor(recipe.resultType);
    SpawnPieceAt(recipe.resultType, false, resultColor, spawnPos);

    Vector3 popupPos = spawnPos + Vector3.up * 1.1f;
    DamagePopup.Create(popupPos, $"{recipe.recipeName}！", DamagePopupType.Critical);

    Debug.Log($"⚔️【異種合成】{recipe.recipeName}（{recipe.materialType1}×{recipe.materialCount1} + {recipe.materialType2}×{recipe.materialCount2} → {recipe.resultType}）");
  }

  // 課題【異種合成】: 指定した駒種の「★1・生存中の自駒」をcount体集めて返す（UI_GetFusionCandidatesと同じ条件）
  List<PieceData> CollectMaterial(PieceType type, int count)
  {
    List<PieceData> result = new List<PieceData>();
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy || p.currentHp <= 0 || p.rank != 1 || p.type != type) continue;
      result.Add(p);
      if (result.Count == count) break;
    }
    return result;
  }

  // ─────────────────────────────────────────────────────────
  // 課題【合成/融合の手動選択モード】: 選択モードの開始・操作・確定・中断API
  // ─────────────────────────────────────────────────────────

  // 同種合成/★3進化の選択開始（MergeButtonUIから呼ばれる）
  public void UI_StartMergeSelection(PieceType type, int fromRank)
  {
    isSelectionModeActive = true;
    selectionFromRank = fromRank;
    selectionFusionRecipeIndex = -1;
    selectionRequirements = new List<SelectionRequirement>
    {
      new SelectionRequirement { type = type, fromRank = fromRank, requiredCount = 3 }
    };
    selectedPieces.Clear();
    RefreshAllHighlights();
  }

  // 異種融合の選択開始（FusionButtonUIから呼ばれる）
  public void UI_StartFusionSelection(int recipeIndex)
  {
    if (fusionRecipeData == null || recipeIndex < 0 || recipeIndex >= fusionRecipeData.recipes.Count) return;
    var recipe = fusionRecipeData.recipes[recipeIndex];

    isSelectionModeActive = true;
    selectionFromRank = 0;
    selectionFusionRecipeIndex = recipeIndex;
    selectionRequirements = new List<SelectionRequirement>
    {
      new SelectionRequirement { type = recipe.materialType1, fromRank = 1, requiredCount = recipe.materialCount1 },
      new SelectionRequirement { type = recipe.materialType2, fromRank = 1, requiredCount = recipe.materialCount2 }
    };
    selectedPieces.Clear();
    RefreshAllHighlights();
  }

  // 候補取得（一覧リストUIから呼ばれる。盤面・ベンチ問わず対象を返す）
  public List<PieceData> UI_GetSelectionCandidates()
  {
    List<PieceData> result = new List<PieceData>();
    if (!isSelectionModeActive) return result;

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.isEnemy || p.currentHp <= 0) continue;
      foreach (var req in selectionRequirements)
      {
        if (p.type == req.type && p.rank == req.fromRank)
        {
          result.Add(p);
          break;
        }
      }
    }
    return result;
  }

  // トグル選択（盤面クリック・一覧リストの両方から呼ばれる共通口）
  public void UI_ToggleSelectionForPiece(PieceData piece)
  {
    if (!isSelectionModeActive || piece == null) return;
    if (piece.isEnemy) return; // 選択モードの対象は常に自駒のみ（UI_GetSelectionCandidatesと同じ前提）

    if (selectedPieces.Contains(piece))
    {
      selectedPieces.Remove(piece);
    }
    else
    {
      // この駒がどの条件に該当するか探し、その条件の必要数をまだ満たしていなければ追加する
      foreach (var req in selectionRequirements)
      {
        if (piece.type != req.type || piece.rank != req.fromRank) continue;

        int currentCountForReq = selectedPieces.FindAll(p => p.type == req.type && p.rank == req.fromRank).Count;
        if (currentCountForReq < req.requiredCount)
        {
          selectedPieces.Add(piece);
        }
        break;
      }
    }

    RefreshAllHighlights();
  }

  public bool UI_IsPieceSelected(PieceData piece) => selectedPieces.Contains(piece);
  public bool UI_IsSelectionModeActive() => isSelectionModeActive;

  // 一覧リストUIが「ポーン ★2: 2/3」のような進捗表示に使う
  public List<(PieceType type, int fromRank, int current, int required)> UI_GetSelectionProgress()
  {
    var result = new List<(PieceType, int, int, int)>();
    foreach (var req in selectionRequirements)
    {
      int current = selectedPieces.FindAll(p => p.type == req.type && p.rank == req.fromRank).Count;
      result.Add((req.type, req.fromRank, current, req.requiredCount));
    }
    return result;
  }

  // 全ての条件の必要数を満たしているかどうか（確定ボタンのinteractable制御等に使う）
  public bool UI_IsSelectionComplete()
  {
    if (!isSelectionModeActive) return false;

    foreach (var req in selectionRequirements)
    {
      int current = selectedPieces.FindAll(p => p.type == req.type && p.rank == req.fromRank).Count;
      if (current < req.requiredCount) return false;
    }
    return true;
  }

  // 確定ボタン押下時: 選択済みの駒で実際に合成/進化/融合を実行し、選択モードを終了する
  public void UI_ConfirmSelection()
  {
    if (!isSelectionModeActive || !UI_IsSelectionComplete()) return;

    if (selectionFusionRecipeIndex >= 0)
    {
      // 異種融合: 選択済みの駒を、レシピのmaterialType1/2ごとに振り分けてExecuteFusionCoreへ渡す
      FusionRecipeEntry recipe = fusionRecipeData.recipes[selectionFusionRecipeIndex];

      List<PieceData> materials1 = selectedPieces.FindAll(p => p.type == recipe.materialType1);
      List<PieceData> materials2 = selectedPieces.FindAll(p => p.type == recipe.materialType2);
      // 必要数ちょうどに切り詰める（selectedPiecesは既にUI_ToggleSelectionForPiece側で
      // 各条件の必要数を超えては追加されない設計のため、通常はこの時点で既に必要数と一致している）
      if (materials1.Count > recipe.materialCount1) materials1 = materials1.GetRange(0, recipe.materialCount1);
      if (materials2.Count > recipe.materialCount2) materials2 = materials2.GetRange(0, recipe.materialCount2);

      ExecuteFusionCore(recipe, materials1, materials2);
    }
    else if (selectionFromRank == 1)
    {
      ExecuteMerge(new List<PieceData>(selectedPieces));
    }
    else if (selectionFromRank == 2)
    {
      ExecuteStar3Evolution(new List<PieceData>(selectedPieces));
    }

    EndSelectionMode();
  }

  // 中断ボタン押下時: 何も実行せず選択モードを終了する
  public void UI_CancelSelection()
  {
    EndSelectionMode();
  }

  void EndSelectionMode()
  {
    isSelectionModeActive = false;
    selectionRequirements.Clear();
    selectedPieces.Clear();
    selectionFromRank = 0;
    selectionFusionRecipeIndex = -1;
    RefreshAllHighlights(); // isSelectionModeActive=falseになったため、全駒のハイライトがNoneへ戻る
  }

  // 選択モードの状態（対象外/選択可能/選択済み）に応じて、盤上・ベンチ問わず全ての自駒のハイライトを更新する
  void RefreshAllHighlights()
  {
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.currentHp <= 0) continue;

      if (!isSelectionModeActive)
      {
        p.SetSelectionHighlight(SelectionHighlightState.None);
        continue;
      }

      if (selectedPieces.Contains(p))
      {
        p.SetSelectionHighlight(SelectionHighlightState.Selected);
      }
      else if (IsEligibleForSelection(p))
      {
        p.SetSelectionHighlight(SelectionHighlightState.Eligible);
      }
      else
      {
        p.SetSelectionHighlight(SelectionHighlightState.None);
      }
    }
  }

  bool IsEligibleForSelection(PieceData piece)
  {
    if (piece.isEnemy) return false;
    foreach (var req in selectionRequirements)
    {
      if (piece.type == req.type && piece.rank == req.fromRank) return true;
    }
    return false;
  }

  void GenerateGrowthOptions()
  {
    // ステップ24: GrowthBonusDataSOにエントリがあればその選択肢一覧を候補プールとして使用する。
    // 未設定/エントリ空の場合は既存のハードコード4種へフォールバックする。
    List<GrowthType> pool = new List<GrowthType>();

    if (growthBonusData != null && growthBonusData.options.Count > 0)
    {
      foreach (var entry in growthBonusData.options)
      {
        pool.Add(entry.type);
      }
    }
    else
    {
      pool.Add(GrowthType.AttackUp);
      pool.Add(GrowthType.HpUp);
      pool.Add(GrowthType.SpeedUp);
      pool.Add(GrowthType.Lifesteal);
    }

    for (int i = 0; i < pool.Count; i++)
    {
      int rand = Random.Range(i, pool.Count);
      GrowthType temp = pool[i];
      pool[i] = pool[rand];
      pool[rand] = temp;
    }

    // ステップ25【不具合修正】: 従来は候補が3未満の場合に pool[i % pool.Count] で水増ししており、
    // 同じ GrowthType がモーダルに重複して並んでしまう不具合があった。
    // 重複を避けるため、シャッフル済みの重複のない候補を先頭から最大3件だけそのまま採用する
    // （候補が1〜2件しか無い場合は、その件数分だけ選択肢として表示される）。
    growthOptions.Clear();
    int pickCount = Mathf.Min(3, pool.Count);
    for (int i = 0; i < pickCount; i++)
    {
      growthOptions.Add(pool[i]);
    }
  }

  // ステップ23: UGUI（GrowthModalUI）から参照する状態取得API
  public bool UI_IsGrowthModalOpen() => showGrowthModal && evolvingPiece != null;
  public string UI_GetEvolvingPieceName() => evolvingPiece != null ? evolvingPiece.pieceName : "";
  public GrowthType[] UI_GetGrowthOptions() => growthOptions.ToArray();
  public void UI_ApplyGrowthChoice(GrowthType choice) => ApplyGrowthChoice(choice);

  // GrowthTypeごとの表示用タイトル・説明文（既存OnGUI版のswitch文をそのまま踏襲）
  public void UI_GetGrowthOptionLabel(GrowthType type, out string title, out string desc)
  {
    // ステップ24: GrowthBonusDataSOに該当タイプのエントリがあればそちらの表示文言を使用する
    GrowthBonusDataSO.GrowthBonusEntry entry = growthBonusData != null ? growthBonusData.GetEntry(type) : null;
    if (entry != null)
    {
      title = entry.title;
      desc = entry.description;
      return;
    }

    // ---- フォールバック: 既存のハードコードロジック ----
    switch (type)
    {
      case GrowthType.AttackUp:
        title = "攻撃強化";
        desc = "攻撃力 +150";
        break;
      case GrowthType.HpUp:
        title = "耐久強化";
        desc = "最大HP +1000";
        break;
      case GrowthType.SpeedUp:
        title = "敏捷強化";
        desc = "攻撃速度 +20%";
        break;
      case GrowthType.Lifesteal:
        title = "吸血付与";
        desc = "攻撃時 20% 吸血";
        break;
      default:
        title = "";
        desc = "";
        break;
    }
  }

  void ApplyGrowthChoice(GrowthType choice)
  {
    Vector3 popupPos = evolvingPiece.transform.position + Vector3.up * 0.9f;

    // ステップ24: GrowthBonusDataSOに該当タイプのエントリがあればその数値を使用する。
    // 未設定/該当エントリ無しの場合は既存のハードコード値へフォールバックする。
    GrowthBonusDataSO.GrowthBonusEntry entry = growthBonusData != null ? growthBonusData.GetEntry(choice) : null;

    switch (choice)
    {
      case GrowthType.AttackUp:
      {
        int amount = entry != null ? Mathf.RoundToInt(entry.value) : 150;
        evolvingPiece.attack += amount;
        DamagePopup.Create(popupPos, $"ATK +{amount}", DamagePopupType.Critical);
        break;
      }

      case GrowthType.HpUp:
      {
        int amount = entry != null ? Mathf.RoundToInt(entry.value) : 1000;
        evolvingPiece.maxHp += amount;
        evolvingPiece.currentHp += amount;
        DamagePopup.Create(popupPos, $"HP +{amount}", DamagePopupType.Heal);
        break;
      }

      case GrowthType.SpeedUp:
      {
        float rate = entry != null ? entry.value : 0.2f;
        evolvingPiece.attackInterval *= (1f - rate);
        DamagePopup.Create(popupPos, "SPD UP!", DamagePopupType.Critical);
        break;
      }

      case GrowthType.Lifesteal:
      {
        float rate = entry != null ? entry.value : 0.2f;
        evolvingPiece.lifestealRate += rate;
        DamagePopup.Create(popupPos, $"吸血 +{rate * 100f:F0}%", DamagePopupType.Heal);
        break;
      }
    }

    // 課題【★2→★3合成の育成履歴分岐システム】: ★2進化時に選んだGrowthTypeを履歴として記録する。
    // ★1→★2の既存フロー自体のロジックは変更せず、この1行を追加するのみ。
    evolvingPiece.growthHistory.Add(choice);

    showGrowthModal = false;
    evolvingPiece = null;
  }

  // ==============================
  // ステップ4: ハクスラ装備システム
  // ==============================

  float GetDropChance(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    if (entry != null) return entry.dropChance;

    switch (type)
    {
      case PieceType.Pawn: return 0.15f;
      case PieceType.Knight: return 0.25f;
      case PieceType.Bishop: return 0.30f;
      case PieceType.Rook: return 0.40f;
      case PieceType.Queen: return 0.50f;
      default: return 0.15f;
    }
  }

  int GetQualityTier(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    if (entry != null) return entry.qualityTier;

    switch (type)
    {
      case PieceType.Pawn:
      case PieceType.Knight:
        return 1;
      case PieceType.Bishop:
      case PieceType.Rook:
        return 2;
      case PieceType.Queen:
        return 3;
      default:
        return 1;
    }
  }

  // 敵駒が倒された際にPieceData.TakeDamage()から呼ばれる（ステップ5: 撃破数カウント＋ドロップ判定）
  public void OnEnemyDefeated(PieceType type, Vector3 position)
  {
    totalEnemiesDefeated++;

    // 課題3【コイン獲得処理の一元化】: 敵撃破報酬をGameConfigSOから取得してAddGold経由で付与する。
    // 既定値は0（＝現状の収支バランスを変えない）。SO側で値を設定した場合のみ加算される。
    int killGold = gameConfig != null ? gameConfig.goldPerEnemyKill : 0;
    if (killGold != 0) AddGold(killGold, GoldSourceType.EnemyKill);

    // ステップ5: エンドレスモード中は装備ドロップ獲得を停止
    if (!isEndlessMode)
    {
      TryDropEquipment(type, position);
    }
  }

  void TryDropEquipment(PieceType type, Vector3 position)
  {
    // ステップ29【要件6】: 3択フロア選択（強敵＝ドロップ率UP等）の倍率をドロップ判定に反映する
    // ステップ31【改善】: 倍率適用後の値が100%を超えて振り切れないようクランプする
    float chance = Mathf.Clamp01(GetDropChance(type) * currentWaveDropRateMultiplier);
    if (Random.value <= chance)
    {
      int tier = GetQualityTier(type);
      EquipmentInstance newItem = EquipmentGenerator.GenerateRandomEquipment(tier);
      SpawnDropCube(newItem, position);
    }
  }

  void SpawnDropCube(EquipmentInstance item, Vector3 position)
  {
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.name = $"Drop_{item.rarity}";
    cube.transform.position = new Vector3(position.x, 0.3f, position.z);
    cube.transform.localScale = Vector3.one * 0.3f;
    cube.transform.rotation = Quaternion.Euler(20f, 40f, 0f);

    Renderer ren = cube.GetComponent<Renderer>();
    if (ren != null) ren.material.color = GetRarityColor(item.rarity);

    EquipmentDropPickup pickup = cube.AddComponent<EquipmentDropPickup>();
    pickup.item = item;

    Debug.Log($"💎 ドロップ発生！【{item.itemName}】({item.rarity})");
  }

  // 装備ドロップ品がクリックされた際に呼ばれる（EquipmentDropPickupから）
  public void CollectDrop(EquipmentDropPickup pickup)
  {
    if (pickup == null || pickup.item == null) return;
    AddItemToInventory(pickup.item);
    Destroy(pickup.gameObject);
  }

  // フェーズ終了時、盤上に残っている未回収ドロップを全て自動回収
  void CollectAllRemainingDrops()
  {
    EquipmentDropPickup[] drops = FindObjectsOfType<EquipmentDropPickup>();
    foreach (var d in drops)
    {
      AddItemToInventory(d.item);
      Destroy(d.gameObject);
    }
  }

  // インベントリに空きがあれば追加、満杯なら自動売却してゴールドに変換
  public void AddItemToInventory(EquipmentInstance item)
  {
    if (item == null) return;

    if (inventory.Count < MaxInventorySlots)
    {
      inventory.Add(item);
      Debug.Log($"装備【{item.itemName}】をインベントリに追加！");
    }
    else
    {
      int sellValue = GetSellValue(item.rarity);
      AddGold(sellValue, GoldSourceType.EquipmentAutoSell);
      Debug.Log($"インベントリ満杯のため【{item.itemName}】を自動売却（+{sellValue}G）");
    }
  }

  int GetSellValue(EquipmentRarity rarity)
  {
    if (gameConfig != null)
    {
      switch (rarity)
      {
        case EquipmentRarity.Common: return gameConfig.sellValueCommon;
        case EquipmentRarity.Rare: return gameConfig.sellValueRare;
        case EquipmentRarity.Epic: return gameConfig.sellValueEpic;
        case EquipmentRarity.Legendary: return gameConfig.sellValueLegendary;
      }
    }

    switch (rarity)
    {
      case EquipmentRarity.Common: return 100;
      case EquipmentRarity.Rare: return 200;
      case EquipmentRarity.Epic: return 400;
      case EquipmentRarity.Legendary: return 700;
      default: return 100;
    }
  }

  // インベントリの装備を指定した駒へ装着する
  public void EquipItemFromInventory(EquipmentInstance item, PieceData piece)
  {
    if (piece == null)
    {
      Debug.LogWarning("⚠️ 装備を付与する駒が選択されていません。先に駒をクリックして選択してください。");
      return;
    }

    if (item == null)
    {
      Debug.LogWarning("⚠️ 装備しようとしたアイテムが無効（null）です。");
      return;
    }

    if (!inventory.Contains(item))
    {
      Debug.LogWarning($"⚠️ 【{item.itemName}】はインベントリに見つかりません（既に消費済みの可能性があります）。");
      return;
    }

    if (!piece.CanEquip(item))
    {
      if (piece.currentHp <= 0)
      {
        Debug.LogWarning($"⚠️ {piece.pieceName} は戦闘不能のため装備できません。");
      }
      else
      {
        Debug.LogWarning($"⚠️ {piece.pieceName} の装備枠は満杯です（最大 {piece.EffectiveMaxEquipSlots}）！");
      }
      return;
    }

    inventory.Remove(item);
    piece.EquipItem(item);
  }

  // 駒から装備を外し、インベントリへ戻す（満杯なら自動売却）
  public void UnequipItemFromPiece(PieceData piece, EquipmentInstance item)
  {
    if (piece == null || item == null) return;
    if (!piece.equippedItems.Contains(item)) return;

    piece.UnequipItem(item);
    AddItemToInventory(item);
  }

  // ステップ18: UGUI（InventoryUI / PieceInspectPanel）から参照するための公開API
  public int UI_MaxInventorySlots => MaxInventorySlots;
  public Color UI_GetRarityColor(EquipmentRarity rarity) => GetRarityColor(rarity);
  public float UI_GetAttackRange(PieceType type) => GetAttackRange(type);

  // 選択中の駒に対してインベントリのアイテムを装着する（駒未選択時はfalseを返す）
  public bool UI_TryEquipToSelectedPiece(EquipmentInstance item)
  {
    if (selectedPiece == null)
    {
      Debug.LogWarning("⚠️ 装備を付与する駒を先に選択してください。");
      return false;
    }

    EquipItemFromInventory(item, selectedPiece);
    return true;
  }

  Color GetRarityColor(EquipmentRarity rarity)
  {
    switch (rarity)
    {
      case EquipmentRarity.Common: return Color.white;
      case EquipmentRarity.Rare: return new Color(0.3f, 0.6f, 1.0f);
      case EquipmentRarity.Epic: return new Color(0.7f, 0.3f, 1.0f);
      case EquipmentRarity.Legendary: return new Color(1.0f, 0.85f, 0.2f);
      default: return Color.white;
    }
  }

  // ステップ18: プレイヤー向けインベントリUIはUGUI（InventoryUI.cs）へ移行したため、
  // 旧OnGUI版のDrawInventoryUIは削除しました。

  // 課題1【復活(リバース)システム】: UnitStatusDataSOから駒種ごとの復活パラメータを取得する。
  // 未設定/該当エントリ無しの場合は「復活しない(rate=0)」を既定値としてフォールバックする
  // （＝この機能を追加しても、SOを設定しない限り既存の挙動は一切変わらない）。
  public float GetRebirthRate(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    return entry != null ? entry.rebirthRate : 0f;
  }

  public float GetRebirthHpRatio(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    return entry != null ? entry.rebirthHpRatio : 0.3f;
  }

  public float GetRebirthInvincibleSeconds(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    return entry != null ? entry.rebirthInvincibleSeconds : 1.0f;
  }

  int GetCost(PieceType type)
  {
    UnitStatusDataSO.UnitStatusEntry entry = unitStatusData != null ? unitStatusData.GetStats(type) : null;
    if (entry != null) return entry.shopCost;

    switch (type)
    {
      case PieceType.Pawn: return 100;
      case PieceType.Knight: return 200;
      case PieceType.Bishop: return 200;
      case PieceType.Rook: return 300;
      case PieceType.Queen: return 400;
      default: return 100;
    }
  }

  void RerollShop(bool consumeGold)
  {
    if (consumeGold)
    {
      // ステップ27【要件1】: エンドレスモード中も有償リロールを可能にする（報酬のみ0のまま）
      if (gold < RerollCost) return;
      AddGold(-RerollCost, GoldSourceType.ShopReroll);    }

    List<PieceType> pool = new List<PieceType> { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen };

    for (int i = 0; i < pool.Count; i++)
    {
      int rand = Random.Range(i, pool.Count);
      PieceType temp = pool[i];
      pool[i] = pool[rand];
      pool[rand] = temp;
    }

    for (int i = 0; i < 3; i++) shopItems[i] = pool[i];
  }

  void BuyPiece(int index)
  {
    // ステップ27【要件1】: エンドレスモード中もショップでの購入を可能にする（報酬のみ0のまま）

    PieceType type = shopItems[index];
    int cost = GetCost(type);

    if (gold < cost) return;

    Vector3? benchPos = FindEmptyBenchPosition();
    if (benchPos == null) return;

    AddGold(-cost, GoldSourceType.ShopPurchase);
    SpawnPieceAt(type, false, GetPieceColor(type), benchPos.Value);
  }

  void SpawnPieceToBenchOrBoard(PieceType type, bool isEnemy, Color color)
  {
    Vector3? benchPos = FindEmptyBenchPosition();
    Vector3 spawnPos = benchPos ?? GridToWorldPosition(Random.Range(1, 7), 1, 0.25f);
    SpawnPieceAt(type, isEnemy, color, spawnPos);
  }

  public Vector3? FindEmptyBenchPosition()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え

    // 【ベンチの切り分け】走査範囲はBoardDepthではなく、ベンチ専用のBenchSlotCountを使用する
    for (int i = 0; i < BenchSlotCount; i++)
    {
      Vector3 targetLocal = GetBenchLocalPosition(i);
      bool isOccupied = false;

      foreach (var p in PieceRegistry.AllPieces)
      {
        if (p.currentHp <= 0) continue;

        Vector3 pLocal = WorldToBoardLocal(p.transform.position);
        if (Mathf.Abs(pLocal.x - targetLocal.x) < 0.3f &&
            Mathf.Abs(pLocal.z - targetLocal.z) < 0.3f)
        {
          isOccupied = true;
          break;
        }
      }

      if (!isOccupied) return BenchGridToWorldPosition(i, 0.25f);
    }
    return null;
  }

  Color GetPieceColor(PieceType type)
  {
    switch (type)
    {
      case PieceType.Pawn: return Color.white;
      case PieceType.Knight: return Color.cyan;
      case PieceType.Bishop: return new Color(0.2f, 1.0f, 0.8f);
      case PieceType.Rook: return new Color(0.2f, 0.6f, 1.0f);
      case PieceType.Queen: return new Color(0.8f, 0.3f, 1.0f);
      default: return Color.white;
    }
  }

  public void StartBattle()
  {
    ApplyFormationSynergies();

    // ステップ8【バグ修正】: 戦闘開始ボタンが押された「その瞬間」の位置を、盤上・ベンチ問わず
    // 全ての味方駒について記憶する。ベンチ上の駒も対象に含めることで、
    // 「過去に盤上にいた頃の古い座標」が残ったまま戦闘後にワープしてしまう不具合を防ぐ。
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 【フィルタ確認】ここにcurrentHpによる生存フィルタは無いが、前ウェーブの死亡した自駒は
    // 既にCleanUpBattlefield()側で「真の死亡（Destroy済み＝Registryから除外済み）」か
    // 「瀕死生存（currentHp=1に復帰済み）」のどちらかに解決されているため、
    // この時点でRegistryに残っている自駒は実質的に必ずcurrentHp>0であり、挙動は変わらない。
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (!p.isEnemy)
      {
        p.savedPosition = p.transform.position;
      }
    }

    isBattleStarted = true;
  }

  public void ResetScene()
  {
    Time.timeScale = 1f; // ステップ6: 倍速設定はシーンリロードを跨いで残るため明示的にリセット
    Scene currentScene = SceneManager.GetActiveScene();
    SceneManager.LoadScene(currentScene.name);
  }

  // ステップ6: 早送り倍率の切り替え
  void SetSpeed(int index)
  {
    currentSpeedIndex = index;
    if (!isSkipping)
    {
      Time.timeScale = speedOptions[currentSpeedIndex];
    }
  }

  // ステップ6: スキップボタン。戦闘中のみ有効。タイムスケールを極大化して一瞬で決着させる
  void ToggleSkip()
  {
    if (!isBattleStarted || isGameOver) return;
    if (isSkipping) return;

    isSkipping = true;
    Time.timeScale = SkipTimeScale;
  }

  // =====================================================================
  // ステップ11: UIManager（UGUI）から呼び出すための公開API
  // 内部の実装（private メソッド/フィールド）は変更せず、外部公開用の薄いラッパーとして提供する。
  // =====================================================================

  public void UI_StartBattle() => StartBattle();
  public void UI_BuyPiece(int index) => BuyPiece(index);
  public void UI_RerollShop() => RerollShop(true);
  public void UI_SetSpeed(int index) => SetSpeed(index);
  public void UI_ToggleSkip() => ToggleSkip();
  public void UI_ToggleSkillTree() => showSkillTreeModal = !showSkillTreeModal;
  public void UI_ToggleCemetery() => showCemeteryModal = !showCemeteryModal;
  public void UI_ToggleDebugMenu() => showDebugMenu = !showDebugMenu;

  public bool UI_IsSkillTreeModalOpen() => showSkillTreeModal;
  public bool UI_IsCemeteryModalOpen() => showCemeteryModal;

  // ステップ13: 旧OnGUI版DrawSkillTreeModal内にあったスキル強化ロジックを、
  // UIManager（UGUIボタン）から呼び出せる公開APIとして独立させたもの。
  public void UI_UpgradeAura()
  {
    // ステップ27【要件1】: エンドレスモード中もSP振りを可能にする（SP獲得自体はCheckBattleResultで0になる）
    if (skillPoints < 1) return;
    skillPoints--;
    skillAuraLevel++;
  }

  public void UI_UpgradeEconomy()
  {
    if (skillPoints < 1) return;
    skillPoints--;
    skillEconomyLevel++;
  }

  public void UI_UpgradeBarrier()
  {
    if (skillPoints < 1) return;
    skillPoints--;
    skillBarrierLevel++;
  }

  public PieceType UI_GetShopItemType(int index) => shopItems[index];
  public int UI_GetShopItemCost(int index) => GetCost(shopItems[index]);
  public int UI_GetSpeedOptionCount() => speedOptions.Length;
  public float UI_GetSpeedOption(int index) => speedOptions[index];
  public int UI_GetCurrentSpeedIndex() => currentSpeedIndex;
  public bool UI_IsSkipping() => isSkipping;

  public string UI_GetWaveLabel()
  {
    return isEndlessMode ? $"【ENDLESS】Wave {currentWave}" : $"Wave {currentWave}";
  }

  public string UI_GetHighScoreLabel()
  {
    return $"ハイスコア: {ScoreManager.GetHighScore()} pt (Wave {ScoreManager.GetHighScoreWave()})  |  撃破数: {totalEnemiesDefeated}";
  }

  // ステップ12/16: ベンチ枠（0〜7）が生存駒で埋まっているかどうかをUIから参照するためのAPI
  // 【重大バグ修正】以前のベンチはZ軸方向に並んでいたため「Zだけ」を比較すればスロットを識別できたが、
  // 「ベンチの分離」対応でベンチをX軸方向の横一列（Zは全スロット共通の固定値）に再設計した際、
  // この判定がZ座標のみの比較のままだったため、盤面奥行き方向の位置だけでは
  // 「どのスロットか」を全く区別できなくなっていた（ベンチ内のどのスロットもZはほぼ同一のため）。
  // 結果として、ベンチに1体でも駒を置くと、そのZ座標がどのslotIndexに対しても「一致」してしまい、
  // UI_IsBenchSlotOccupied(i) が全てのiに対してtrueを返す（＝全スロットが「占有中」と誤判定される）
  // というバグが発生していた。X座標もあわせて比較することで、正しく個別のスロットを識別する。
  public bool UI_IsBenchSlotOccupied(int slotIndex)
  {
    Vector3 targetLocal = GetBenchLocalPosition(slotIndex);

    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.currentHp <= 0) continue;
      if (!IsWorldPositionInBenchArea(p.transform.position)) continue;

      Vector3 pLocal = WorldToBoardLocal(p.transform.position);
      if (Mathf.Abs(pLocal.x - targetLocal.x) < 0.3f && Mathf.Abs(pLocal.z - targetLocal.z) < 0.3f) return true;
    }

    return false;
  }

  void SpawnKing(bool isEnemy, Vector3 worldPosition)
  {
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.name = isEnemy ? "Enemy_King" : "Player_King";

    // ステップ16: BoardTransformの子にしてローカル座標で配置することで、
    // 以後BoardParentの位置・回転・スケールを変更しても自動的に追従する
    cube.transform.SetParent(BoardTransform, false);
    cube.transform.localPosition = WorldToBoardLocal(worldPosition);
    cube.transform.localRotation = Quaternion.identity;
    cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

    Renderer ren = cube.GetComponent<Renderer>();
    if (ren != null) ren.material.color = isEnemy ? new Color(0.5f, 0f, 0f) : new Color(0f, 0.8f, 0f);

    PieceData data = cube.AddComponent<PieceData>();
    data.type = PieceType.King;
    data.isEnemy = isEnemy;

    cube.AddComponent<PieceAI>();
    cube.AddComponent<PieceDraggable>();
    cube.AddComponent<PieceTooltipTrigger>(); // 課題【PieceTooltipTriggerの自動アタッチ漏れ修正】
  }

  void SpawnPieceAt(PieceType type, bool isEnemy, Color color, Vector3 worldPosition, AIBehaviorDataSO aiBehavior = null)
  {
    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cube.name = isEnemy ? $"Enemy_{type}" : $"Player_{type}";

    // ステップ16: BoardTransformの子にしてローカル座標で配置（BoardParentの変更に自動追従させるため）
    cube.transform.SetParent(BoardTransform, false);
    cube.transform.localPosition = WorldToBoardLocal(worldPosition);
    cube.transform.localRotation = Quaternion.identity;
    cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

    Renderer ren = cube.GetComponent<Renderer>();
    if (ren != null) ren.material.color = color;

    PieceData data = cube.AddComponent<PieceData>();
    data.type = type;
    data.isEnemy = isEnemy;
    data.aiBehavior = aiBehavior; // 課題【AIパターンのSO管理化】: null（バランス型）がデフォルト

    cube.AddComponent<PieceAI>();
    cube.AddComponent<PieceDraggable>();
    cube.AddComponent<PieceTooltipTrigger>(); // 課題【PieceTooltipTriggerの自動アタッチ漏れ修正】: 敵駒・プレイヤー駒の両方に等しくアタッチする
  }

  // =====================================================================
  // 課題3【敵駒の初期配置エリアの制限】
  // 敵駒がスポーンし得るgridZの範囲を「盤面奥側2列」（既定: BoardDepth=8, EnemyBackRowDepth=2 → Z=6〜7）に限定する。
  // EnemyWaveDataSO（Inspectorで編集可能なデータ）や、その他の呼び出し元がどんな値を渡してきても、
  // 最終的なスポーン処理（SpawnEnemyAtGrid / SpawnPiece）でこの範囲へ必ずクランプされるため、
  // 「敵がプレイヤー側の手前の行に出現してしまう」ことは起こり得ない（多重の安全策）。
  // =====================================================================

  // 敵のスポーンに許可されたgridZの範囲 (zMin, zMax) を返す（盤面奥側 EnemyBackRowDepth 行分）
  (int zMin, int zMax) GetEnemySpawnZRange()
  {
    int depth = BoardDepth;
    int rows = EnemyBackRowDepth;
    int zMax = depth - 1;
    int zMin = depth - rows;
    return (zMin, zMax);
  }

  // ステップ16: 引数を「ワールド座標」ではなく「盤面グリッドインデックス(0〜BoardWidth-1, 0〜BoardDepth-1)」に変更。
  // BoardParentの位置・回転・スケールに関わらず、常に正しいマスの上に生成される。
  void SpawnEnemyAtGrid(PieceType type, int gridX, int gridZ, AIBehaviorDataSO aiBehavior = null)
  {
    Color enemyColor = new Color(1.0f, 0.3f, 0.3f);
    switch (type)
    {
      case PieceType.Knight: enemyColor = new Color(1.0f, 0.4f, 0.2f); break;
      case PieceType.Rook: enemyColor = new Color(0.8f, 0.2f, 0.2f); break;
      case PieceType.Bishop: enemyColor = new Color(0.9f, 0.3f, 0.5f); break;
      case PieceType.Queen: enemyColor = new Color(0.6f, 0.0f, 0.4f); break;
    }

    // 課題3: EnemyWaveDataSO（またはフォールバックのハードコードswitch）から渡されたgridZが
    // 盤面奥側2列の範囲外だったとしても、ここで必ずクランプする。
    int clampedGridX = Mathf.Clamp(gridX, 0, BoardWidth - 1);
    var (zMin, zMax) = GetEnemySpawnZRange();
    int clampedGridZ = Mathf.Clamp(gridZ, zMin, zMax);

    SpawnPieceAt(type, true, enemyColor, GridToWorldPosition(clampedGridX, clampedGridZ, 0.25f), aiBehavior);
  }

  void SpawnPiece(PieceType type, bool isEnemy, Color color)
  {
    int gridX = Random.Range(0, BoardWidth);
    int gridZ;

    if (isEnemy)
    {
      // 課題3: デバッグメニューからの手動追加も含め、敵は必ず盤面奥側の範囲内にのみ生成する
      var (zMin, zMax) = GetEnemySpawnZRange();
      gridZ = Random.Range(zMin, zMax + 1);
    }
    else
    {
      gridZ = 1; // 味方: 従来と同じ位置（自陣内、gridZ=1）を維持
    }

    Vector3 pos = GridToWorldPosition(gridX, gridZ, 0.25f);
    SpawnPieceAt(type, isEnemy, color, pos);
  }
}
