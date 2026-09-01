using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ13: UI管理を「コードによる動的生成」から「Editor上のCanvas Prefab + Inspector参照」方式へ全面移行。
// ステップ14: テキスト表示をUnity標準Textから TextMeshProUGUI に統一。
// このスクリプトはUI要素を一切生成・Instantiateしない。あらかじめSceneまたはPrefabとして構築した
// Canvas階層の各要素を、Inspectorからドラッグ＆ドロップで割り当てることで動作する。
// 設定手順は本回答末尾のセットアップ手順書を参照。
public class UIManager : MonoBehaviour
{
  public static UIManager Instance { get; private set; }

  [Header("Top Panel")]
  [SerializeField] private TextMeshProUGUI waveText;
  [SerializeField] private TextMeshProUGUI playerHpText;
  [SerializeField] private TextMeshProUGUI goldText;
  [SerializeField] private Button speedButton;   // クリックのたびに 1x → 2x → 4x → 1x… と循環する単一ボタン
  [SerializeField] private Button skipButton;
  [SerializeField] private Button cemeteryButton;
  [SerializeField] private Button skillButton;

  [Header("キングスキル機能（仕様変更により既定で無効）")]
  [Tooltip("falseの場合、キングスキルボタンは起動時に自動的に非表示になり、クリックしても何も起こりません。" +
           "スキルツリーモーダルも常に非表示に強制されるため、誤って空/未設定のモーダルパネル（全画面Panel等）が" +
           "開いたままになって操作不能になる事故を防げます。機能を復活させる場合はtrueにしてください。")]
  [SerializeField] private bool kingSkillFeatureEnabled = false;

  [Header("Bottom Panel")]
  [SerializeField] private GameObject bottomPanel;   // 準備フェーズ以外は非表示にするためのルート
  [SerializeField] private Button[] shopButtons;
  [SerializeField] private TextMeshProUGUI[] shopPriceTexts;
  [SerializeField] private Button rerollButton;
  [SerializeField] private Button startBattleButton;
  [SerializeField] private Transform benchContainer; // 子に8個のImage（ベンチ枠インジケーター）を並べておく

  [Header("Modals")]
  [SerializeField] private GameObject cemeteryModal;
  [SerializeField] private TextMeshProUGUI cemeteryContentText;
  [SerializeField] private GameObject skillTreeModal;
  [SerializeField] private TextMeshProUGUI skillPointsText;
  [SerializeField] private Button auraUpgradeButton;
  [SerializeField] private TextMeshProUGUI auraLevelText;
  [SerializeField] private Button economyUpgradeButton;
  [SerializeField] private TextMeshProUGUI economyLevelText;
  [SerializeField] private Button barrierUpgradeButton;
  [SerializeField] private TextMeshProUGUI barrierLevelText;

  [Header("Debug")]
  [SerializeField] private Button debugToggleButton; // F1キーと同じ機能をボタンでも操作できるようにする（任意）

  private DebugGameManager gm;

  // ボタンの子TextMeshProUGUIをキャッシュ（Inspectorで別枠指定していない単純ラベル更新用）
  private TextMeshProUGUI speedButtonLabel;
  private TextMeshProUGUI skipButtonLabel;
  private TextMeshProUGUI cemeteryButtonLabel;
  private TextMeshProUGUI debugToggleButtonLabel;

  void Awake()
  {
    Instance = this;
    CacheButtonLabels();
    RegisterButtonEvents();
    ApplyKingSkillFeatureToggle();
  }

  void Start()
  {
    gm = DebugGameManager.Instance;
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    PullFromGameManager();
  }

  // =====================================================================
  // 初期化
  // =====================================================================

  void CacheButtonLabels()
  {
    if (speedButton != null) speedButtonLabel = speedButton.GetComponentInChildren<TextMeshProUGUI>();
    if (skipButton != null) skipButtonLabel = skipButton.GetComponentInChildren<TextMeshProUGUI>();
    if (cemeteryButton != null) cemeteryButtonLabel = cemeteryButton.GetComponentInChildren<TextMeshProUGUI>();
    if (debugToggleButton != null) debugToggleButtonLabel = debugToggleButton.GetComponentInChildren<TextMeshProUGUI>();
  }

  // ステップ21: キングスキル機能が無効な場合、ボタン自体を非表示にしクリックできないようにする。
  // これにより「廃止済み機能のボタンだけがシーンに残っていて誤って押してしまう」事故を根本から防ぐ。
  // スキルツリーモーダル（skillTreeModal）が誤って全画面パネルとして組まれていた場合でも、
  // ここで強制非表示にすることで画面が白転したまま操作不能になる事故を防止する。
  void ApplyKingSkillFeatureToggle()
  {
    if (kingSkillFeatureEnabled) return;

    if (skillButton != null) skillButton.gameObject.SetActive(false);
    if (skillTreeModal != null) skillTreeModal.SetActive(false);
  }

  // Awake()で各ボタンのonClickを自動登録する（Inspector側のOnClick()設定は不要）
  void RegisterButtonEvents()
  {
    if (speedButton != null) speedButton.onClick.AddListener(OnSpeedButtonClicked);
    if (skipButton != null) skipButton.onClick.AddListener(OnSkipButtonClicked);
    if (cemeteryButton != null) cemeteryButton.onClick.AddListener(OnCemeteryButtonClicked);
    if (skillButton != null) skillButton.onClick.AddListener(OnSkillButtonClicked);
    if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollButtonClicked);
    if (startBattleButton != null) startBattleButton.onClick.AddListener(OnStartBattleClicked);
    if (debugToggleButton != null) debugToggleButton.onClick.AddListener(OnDebugToggleClicked);

    if (shopButtons != null)
    {
      for (int i = 0; i < shopButtons.Length; i++)
      {
        int captured = i;
        if (shopButtons[i] != null)
        {
          shopButtons[i].onClick.AddListener(() => OnShopButtonClicked(captured));
        }
      }
    }

    if (auraUpgradeButton != null) auraUpgradeButton.onClick.AddListener(OnAuraUpgradeClicked);
    if (economyUpgradeButton != null) economyUpgradeButton.onClick.AddListener(OnEconomyUpgradeClicked);
    if (barrierUpgradeButton != null) barrierUpgradeButton.onClick.AddListener(OnBarrierUpgradeClicked);
  }

  // =====================================================================
  // ボタンイベントハンドラ（すべてDebugGameManagerの公開APIへ委譲するだけ）
  // =====================================================================

  void OnSpeedButtonClicked()
  {
    if (gm == null) return;
    int count = gm.UI_GetSpeedOptionCount();
    if (count <= 0) return;
    int next = (gm.UI_GetCurrentSpeedIndex() + 1) % count;
    gm.UI_SetSpeed(next);
  }

  void OnSkipButtonClicked() { if (gm != null) gm.UI_ToggleSkip(); }
  void OnCemeteryButtonClicked() { if (gm != null) gm.UI_ToggleCemetery(); }
  void OnSkillButtonClicked()
  {
    if (!kingSkillFeatureEnabled) return; // ステップ21: 廃止済み機能のため何もしない（安全ガード）
    if (gm != null) gm.UI_ToggleSkillTree();
  }
  void OnRerollButtonClicked() { if (gm != null) gm.UI_RerollShop(); }
  void OnStartBattleClicked() { if (gm != null) gm.UI_StartBattle(); }
  void OnDebugToggleClicked() { if (gm != null) gm.UI_ToggleDebugMenu(); }
  void OnShopButtonClicked(int index) { if (gm != null) gm.UI_BuyPiece(index); }
  void OnAuraUpgradeClicked() { if (gm != null) gm.UI_UpgradeAura(); }
  void OnEconomyUpgradeClicked() { if (gm != null) gm.UI_UpgradeEconomy(); }
  void OnBarrierUpgradeClicked() { if (gm != null) gm.UI_UpgradeBarrier(); }

  // =====================================================================
  // データ反映用の公開メソッド群
  // 既存のゲームロジック側からも直接呼び出せる（例: UIManager.Instance.UpdatePlayerStats(wave, hp, gold, endless)）。
  // 本スクリプト自身もUpdate()から同じメソッドを呼んでいるため、取りこぼしなく毎フレーム同期される。
  // =====================================================================

  public void UpdatePlayerStats(int wave, int hp, int gold, bool isEndlessMode)
  {
    if (waveText != null)
    {
      waveText.text = isEndlessMode ? $"【ENDLESS】Wave {wave}" : $"Wave {wave}";
    }
    if (playerHpText != null) playerHpText.text = $"HP: {hp}";
    if (goldText != null) goldText.text = $"Gold: {gold}G";
  }

  public void UpdateSpeedButton(float currentSpeed, bool isSkipping)
  {
    if (speedButtonLabel != null)
    {
      speedButtonLabel.text = isSkipping ? "" : $"{currentSpeed:0}x";
    }
  }

  public void UpdateSkipButton(bool canSkip, bool isSkipping)
  {
    if (skipButton != null) skipButton.interactable = canSkip;
    if (skipButtonLabel != null) skipButtonLabel.text = isSkipping ? "スキップ中" : "スキップ";
  }

  public void UpdateCemeteryButton(int count)
  {
    if (cemeteryButtonLabel != null) cemeteryButtonLabel.text = $"墓地 ({count})";
  }

  public void UpdateShop(bool endless, int gold)
  {
    if (gm == null || shopButtons == null) return;

    for (int i = 0; i < shopButtons.Length; i++)
    {
      if (shopButtons[i] == null) continue;

      PieceType type = gm.UI_GetShopItemType(i);
      int cost = gm.UI_GetShopItemCost(i);

      // ステップ27【要件1】: エンドレスモード中も購入・リロールを解禁。所持Goldのみで可否を判定する
      bool buyable = gold >= cost;

      shopButtons[i].interactable = buyable;

      TextMeshProUGUI label = shopButtons[i].GetComponentInChildren<TextMeshProUGUI>();
      if (label != null) label.text = $"{type}";

      if (shopPriceTexts != null && i < shopPriceTexts.Length && shopPriceTexts[i] != null)
      {
        shopPriceTexts[i].text = $"{cost}G";
      }

      // 課題【駒特性ツールチップ・ショップ側】: ボタンに未アタッチなら自動でShopSlotTooltipTriggerを付与し、
      // ショップの中身が変わるたび（購入・リロール等でUpdateShopが呼ばれるたび）に現在のPieceTypeを渡し直す。
      // これにより、Inspector側で手動アタッチしなくても全ショップボタンに自動でホバーツールチップが有効になる。
      ShopSlotTooltipTrigger tooltipTrigger = shopButtons[i].GetComponent<ShopSlotTooltipTrigger>();
      if (tooltipTrigger == null) tooltipTrigger = shopButtons[i].gameObject.AddComponent<ShopSlotTooltipTrigger>();
      tooltipTrigger.SetPieceType(type);
    }

    if (rerollButton != null) rerollButton.interactable = gold >= 200;
  }

  // benchContainerの子に並べたBenchSlot数ぶんのImageを、ベンチの占有状況に応じて色分けする。
  // 【3D/2D分離】この関数が触れるのはCanvas上のUI Image（2D側）のみであり、
  // 3D空間上のBenchSlotオブジェクトやそのハイライト用オーバーレイ（DebugGameManager.benchHighlightOverlays）には
  // 一切アクセスしない。3D側の見た目はDebugGameManager.SetBenchHoverIndex/ClearBenchHoverが独立して担当する。
  //
  // 【Indexベースのハイライト】占有中/空き は各slotIndexごとに個別判定した値のみを使う
  // （GetComponentsInChildren等で一括取得した後も、必ずslotIndexを添字にして1件ずつ判定しており、
  //   全件へ同じ値を書き込むような処理にはなっていない）。
  // さらに、DebugGameManager.CurrentBenchHoverIndex（3D側のドラッグで今まさに狙われているスロット）と
  // 一致するIndexだけを、通常の占有色より優先した強調色で上書きする。
  public void UpdateBench()
  {
    if (benchContainer == null || gm == null) return;

    int hoverIndex = gm.CurrentBenchHoverIndex; // -1 = どのスロットもホバーされていない

    int slotIndex = 0;
    foreach (Transform child in benchContainer)
    {
      Image img = child.GetComponent<Image>();
      if (img == null) { slotIndex++; continue; }

      bool occupied = gm.UI_IsBenchSlotOccupied(slotIndex); // このslotIndex「だけ」の占有判定
      bool isHovered = (slotIndex == hoverIndex);            // このslotIndex「だけ」がホバー対象か

      if (isHovered)
      {
        // ドラッグ中カーソルが指している、まさにそのスロットだけを強調表示する
        img.color = new Color(1f, 0.9f, 0.2f, 1f);
      }
      else
      {
        img.color = occupied ? new Color(0.3f, 0.75f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.25f);
      }

      slotIndex++;
    }
  }

  public void UpdateSkillTree(int skillPoints, int auraLevel, int economyLevel, int barrierLevel)
  {
    if (skillPointsText != null) skillPointsText.text = $"所持SP: {skillPoints}";

    if (auraLevelText != null) auraLevelText.text = $"指揮のオーラ (Lv {auraLevel})  全味方の攻撃力 +{auraLevel * 5}";
    if (economyLevelText != null) economyLevelText.text = $"富の知識 (Lv {economyLevel})  ウェーブ開始時 +{economyLevel * 2}G";
    if (barrierLevelText != null) barrierLevelText.text = $"王の加護 (Lv {barrierLevel})  戦闘開始時 全味方HP +{barrierLevel * 30}";

    // ステップ27【要件1】: エンドレスモード中もSP振りを解禁。所持SPのみで可否を判定する
    bool canUpgrade = skillPoints >= 1;
    if (auraUpgradeButton != null) auraUpgradeButton.interactable = canUpgrade;
    if (economyUpgradeButton != null) economyUpgradeButton.interactable = canUpgrade;
    if (barrierUpgradeButton != null) barrierUpgradeButton.interactable = canUpgrade;
  }

  public void UpdateCemeteryContent(List<CemeteryRecord> records)
  {
    if (cemeteryContentText == null) return;

    if (records == null || records.Count == 0)
    {
      cemeteryContentText.text = "まだ戦死した駒はいません。";
      return;
    }

    StringBuilder sb = new StringBuilder();

    // 新しい戦死記録を上に表示
    for (int i = records.Count - 1; i >= 0; i--)
    {
      CemeteryRecord rec = records[i];
      sb.Append($"⚰️ ★{rec.rank} {rec.pieceName}  ―  Wave {rec.deathWave} で戦死\n");

      if (rec.equipmentLog != null && rec.equipmentLog.Count > 0)
      {
        foreach (var eq in rec.equipmentLog)
        {
          sb.Append(eq.wasRecovered ? "    ✅回収: " : "    ❌ロスト: ");
          sb.Append(eq.itemName);
          sb.Append("\n");
        }
      }
    }

    cemeteryContentText.text = sb.ToString();
  }

  // =====================================================================
  // 毎フレームの反映
  // DebugGameManagerの現在値を上記の各UpdateXXXメソッドへ流し込むだけの薄い同期処理。
  // =====================================================================

  void PullFromGameManager()
  {
    UpdatePlayerStats(gm.currentWave, gm.playerHp, gm.gold, gm.isEndlessMode);
    UpdateSpeedButton(gm.UI_GetSpeedOption(gm.UI_GetCurrentSpeedIndex()), gm.UI_IsSkipping());
    UpdateSkipButton(gm.isBattleStarted && !gm.isGameOver, gm.UI_IsSkipping());
    UpdateCemeteryButton(gm.cemeteryList.Count);

    bool isPrepPhase = !gm.isBattleStarted && !gm.isGameOver;
    if (bottomPanel != null) bottomPanel.SetActive(isPrepPhase);

    if (isPrepPhase)
    {
      UpdateShop(gm.isEndlessMode, gm.gold);
      UpdateBench();
    }

    // ステップ21: 機能無効時はDebugGameManager側の状態に関わらず常に閉じたままにする（誤表示防止の二重ガード）
    bool skillOpen = kingSkillFeatureEnabled && gm.UI_IsSkillTreeModalOpen();
    if (skillTreeModal != null) skillTreeModal.SetActive(skillOpen);
    if (skillOpen)
    {
      UpdateSkillTree(gm.skillPoints, gm.skillAuraLevel, gm.skillEconomyLevel, gm.skillBarrierLevel);
    }

    bool cemeteryOpen = gm.UI_IsCemeteryModalOpen();
    if (cemeteryModal != null) cemeteryModal.SetActive(cemeteryOpen);
    if (cemeteryOpen)
    {
      UpdateCemeteryContent(gm.cemeteryList);
    }

    if (debugToggleButtonLabel != null)
    {
      debugToggleButtonLabel.text = gm.showDebugMenu ? "Debug ON (F1)" : "Debug OFF (F1)";
    }
  }
}
