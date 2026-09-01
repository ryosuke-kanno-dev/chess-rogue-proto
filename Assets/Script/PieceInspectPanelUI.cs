using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ19: 駒のステータスパネル（Inspect Panel）のUGUI化。
// DebugGameManager.selectedPiece の変化を毎フレーム監視し、選択中の駒のステータス・バフ・装備一覧を反映する。
// 未選択（null）または死亡/非アクティブになった場合はpanelRootをSetActive(false)にして自動的に隠す。
// ロジック本体（PieceData / EquipmentInstance 等）は一切変更せず、表示・入力の橋渡しのみを行う。
public class PieceInspectPanelUI : MonoBehaviour
{
  [Header("パネル全体")]
  [Tooltip("駒選択時のみ表示するルート。未選択時は自動的にSetActive(false)になる")]
  [SerializeField] private GameObject panelRoot;

  [Header("基本ステータス")]
  [SerializeField] private TextMeshProUGUI nameText;
  [SerializeField] private TextMeshProUGUI hpText;
  [SerializeField] private TextMeshProUGUI attackText;
  [SerializeField] private TextMeshProUGUI speedText;
  [SerializeField] private TextMeshProUGUI rangeText;
  [Tooltip("キングバフ/吸血/連撃/挑発/バフマス/隣接シナジーなどをまとめて複数行表示する（該当する行だけ表示される）")]
  [SerializeField] private TextMeshProUGUI buffText;

  [Header("装備欄")]
  [Tooltip("EquippedItemSlotUIをアタッチした装備スロットのプレハブ")]
  [SerializeField] private GameObject equippedSlotPrefab;
  [SerializeField] private Transform equippedSlotContainer;
  [SerializeField] private TextMeshProUGUI equipHeaderText; // 例:「装備 (2/3)」（任意）

  [Header("操作")]
  [SerializeField] private Button deselectButton;
  [Tooltip("負傷した味方駒をGoldで全回復させるボタン（任意・未設定可）")]
  [SerializeField] private Button healButton;
  [SerializeField] private TextMeshProUGUI healButtonText; // 例:「治療 (2000G)」
  // ステップ31【改善】: healCostはDebugGameManager側（healCostフィールド/UI_GetHealCost()）を
  // 単一の真実とするため、このスクリプト側では保持しない（二重管理の解消）。

  [Header("課題【AIパターンのSO管理化】")]
  [Tooltip("自分の駒(PlayerType.Player)を選択中のみ表示するAIパターン設定ボタン。敵駒インスペクト時はボタンごと非表示にする")]
  [SerializeField] private Button aiBehaviorButton;
  [SerializeField] private TextMeshProUGUI aiBehaviorButtonText; // 例:「AIパターン: バランス型」
  [Tooltip("AIパターン選択ポップアップ（未設定でも動作するが、その場合ボタンを押しても何も起きない）")]
  [SerializeField] private PieceAIBehaviorSelectorModal aiBehaviorSelectorModal;

  [Header("課題【AIパターン表示分離】")]
  [Tooltip("現在のAIパターン名だけを表示する専用テキスト（ボタンの外に新設）")]
  [SerializeField] private TextMeshProUGUI aiBehaviorCurrentText;

  private DebugGameManager gm;
  private readonly List<EquippedItemSlotUI> equippedSlots = new List<EquippedItemSlotUI>();

  void Start()
  {
    gm = DebugGameManager.Instance;

    BuildEquippedSlots();

    if (deselectButton != null)
    {
      deselectButton.onClick.AddListener(OnDeselectClicked);
    }

    if (healButton != null)
    {
      healButton.onClick.AddListener(OnClick_HealPiece);
    }

    if (aiBehaviorButton != null)
    {
      aiBehaviorButton.onClick.AddListener(OnClick_OpenAIBehaviorSelector);
    }

    // 課題【AIパターン表示分離】: ボタン自体のラベルは固定文言にする（動的更新はしない）
    if (aiBehaviorButtonText != null) aiBehaviorButtonText.text = "AIパターン変更";

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    RefreshPanel();
  }

  // ステップ30: 依頼仕様に合わせた公開API。
  // 「指定した駒をパネルへ表示する」明示的な呼び出し口として用意（内部的にはDebugGameManager.selectedPieceを更新するだけ）。
  // 実際の表示監視は既存どおりUpdate()内のRefreshPanel()が毎フレーム自動で行うため、
  // OpenPanel呼び出し直後にも即座に最新表示へ同期するようUpdateUI()を続けて呼んでいる。
  public void OpenPanel(PieceData piece)
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    gm.SelectPiece(piece);
    UpdateUI();
  }

  // ステップ30: 依頼仕様に合わせた公開API。RefreshPanel()の別名（HPゲージ・数値・ボタン活性状態を最新化する）
  public void UpdateUI()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    RefreshPanel();
  }

  // 装備スロットをPieceData.MaxEquipSlots個ぶんだけ起動時に1度だけ生成する。
  // 課題【★2→★3合成の育成履歴分岐システム】: Balanced進化のgrantsExtraEquipSlotにより、
  // 装備スロットの実際の上限（EffectiveMaxEquipSlots）が基礎値より+1される駒が存在するようになったため、
  // プール自体は基礎値+1件分あらかじめ確保しておく（通常の駒では最後の1枠は単に使われず非表示のままになる）。
  void BuildEquippedSlots()
  {
    if (equippedSlotPrefab == null || equippedSlotContainer == null) return;

    int maxSlots = PieceData.MaxEquipSlots + 1;
    for (int i = 0; i < maxSlots; i++)
    {
      GameObject obj = Instantiate(equippedSlotPrefab, equippedSlotContainer);
      obj.name = $"EquippedSlot_{i}";

      EquippedItemSlotUI slot = obj.GetComponent<EquippedItemSlotUI>();
      if (slot == null)
      {
        Debug.LogWarning("⚠️ PieceInspectPanelUI: equippedSlotPrefabにEquippedItemSlotUIがアタッチされていません。");
        continue;
      }

      slot.Initialize(OnLeftClick_ShowDetail, OnUnequipClicked);
      slot.Hide();
      equippedSlots.Add(slot);
    }
  }

  void RefreshPanel()
  {
    PieceData piece = gm.selectedPiece;
    bool isValid = piece != null && piece.gameObject.activeInHierarchy && piece.currentHp > 0;

    if (panelRoot != null) panelRoot.SetActive(isValid);
    if (!isValid) return;

    string starText = piece.rank > 1 ? $" (★{piece.rank})" : "";
    if (nameText != null)
    {
      nameText.text = $"{piece.pieceName}{starText}";
      nameText.color = piece.isEnemy ? Color.red : Color.cyan;
    }

    if (hpText != null) hpText.text = $"HP: {piece.currentHp} / {piece.maxHp}";
    if (attackText != null) attackText.text = $"攻撃力: {piece.attack}";
    if (speedText != null) speedText.text = $"攻撃速度: {piece.attackInterval:F2} 秒";
    if (rangeText != null) rangeText.text = $"攻撃範囲: {gm.UI_GetAttackRange(piece.type):F1}";

    if (buffText != null) buffText.text = BuildBuffSummary(piece);

    if (equipHeaderText != null)
    {
      // 課題【★2→★3合成】: 装備上限の表示もEffectiveMaxEquipSlots（基礎値+ボーナス分）を参照するよう変更
      equipHeaderText.text = $"装備 ({piece.equippedItems.Count}/{piece.EffectiveMaxEquipSlots})";
    }

    RefreshEquippedSlots(piece);
    RefreshHealButton(piece);
    RefreshAIBehaviorButton(piece);
  }

  // 課題【AIパターンのSO管理化】: 自分の駒(PlayerType.Player)を選択中のみボタンを表示し、
  // 課題【AIパターン表示分離】: 現在のaiBehaviorのpatternNameは、ボタンのラベルではなく
  // 専用テキスト（aiBehaviorCurrentText）へ反映する（null＝バランス型）
  void RefreshAIBehaviorButton(PieceData piece)
  {
    if (aiBehaviorButton == null) return;

    bool isOwnPiece = piece.Owner == PlayerType.Player;
    aiBehaviorButton.gameObject.SetActive(isOwnPiece);
    if (!isOwnPiece) return;

    string patternName = piece.aiBehavior != null && !string.IsNullOrEmpty(piece.aiBehavior.patternName)
      ? piece.aiBehavior.patternName
      : "バランス型";

    if (aiBehaviorCurrentText != null)
    {
      aiBehaviorCurrentText.text = patternName;
    }
  }

  // ステップ27【要件4】: 負傷（HP減少中）の自陣駒のみ回復ボタンを有効化する
  void RefreshHealButton(PieceData piece)
  {
    if (healButton == null) return;

    // ステップ31【改善】: コストはDebugGameManager側の単一の真実（UI_GetHealCost()）から取得する
    int cost = gm.UI_GetHealCost();
    bool canHeal = !piece.isEnemy && piece.currentHp > 0 && piece.currentHp < piece.maxHp && gm.gold >= cost;
    healButton.interactable = canHeal;

    if (healButtonText != null)
    {
      healButtonText.text = $"治療 ({cost}G)";
    }
  }

  // 既存OnGUI版のバフ表示条件をそのまま踏襲し、該当する行だけを積み上げて1つのテキストにまとめる
  string BuildBuffSummary(PieceData piece)
  {
    StringBuilder sb = new StringBuilder();

    if (piece.kingBonusAttack > 0) sb.AppendLine($"キングバフ: 攻撃+{piece.kingBonusAttack}");
    if (piece.lifestealRate > 0) sb.AppendLine($"特殊: 吸血 {piece.lifestealRate * 100:F0}%");
    if (piece.doubleAttackChance > 0) sb.AppendLine($"特殊: 連撃 {piece.doubleAttackChance * 100:F0}%");
    if (piece.isTaunting) sb.AppendLine("特殊: 挑発中");
    if (piece.isOnBuffTile) sb.AppendLine("強化: バフマス乗車中");
    if (piece.hasAdjacentBuff) sb.AppendLine("強化: 隣接シナジー発動");

    return sb.ToString();
  }

  void RefreshEquippedSlots(PieceData piece)
  {
    for (int i = 0; i < equippedSlots.Count; i++)
    {
      if (i < piece.equippedItems.Count)
      {
        EquipmentInstance eq = piece.equippedItems[i];

        StringBuilder statLine = new StringBuilder();
        foreach (var b in eq.bonuses)
        {
          statLine.Append(EquipmentGenerator.FormatBonus(b));
          statLine.Append(" ");
        }

        equippedSlots[i].SetData(eq, gm.UI_GetRarityColor(eq.rarity), statLine.ToString());
      }
      else
      {
        equippedSlots[i].Hide();
      }
    }
  }

  // 装備欄の「✖」クリック: 選択中の駒からその装備を外し、インベントリへ戻す
  void OnUnequipClicked(EquipmentInstance item)
  {
    if (gm == null || gm.selectedPiece == null) return;
    gm.UnequipItemFromPiece(gm.selectedPiece, item);
  }

  // 課題【左右クリック分岐】: 装備欄左クリック時、アイテムの詳細（ステータス内訳）をポップアップ表示する
  void OnLeftClick_ShowDetail(EquipmentInstance item)
  {
    if (ItemDetailPopup.Instance == null) return;
    ItemDetailPopup.Instance.Show(item);
  }

  // 「✕ 選択解除」ボタン
  void OnDeselectClicked()
  {
    if (gm == null) return;
    gm.SelectPiece(null);
  }

  // ステップ27【要件4】/ステップ30: 「治療」ボタン。選択中の駒をGoldで全回復する
  void OnClick_HealPiece()
  {
    if (gm == null || gm.selectedPiece == null) return;

    gm.UI_HealPieceWithGold(gm.selectedPiece);

    // ステップ30: 実行後、HPゲージ・数値・ボタン活性状態を即座に最新化する
    UpdateUI();
  }

  // 課題【AIパターンのSO管理化】: 「AIパターン」ボタン押下時、選択ポップアップを開いて現在の駒を渡す
  void OnClick_OpenAIBehaviorSelector()
  {
    if (gm == null || gm.selectedPiece == null) return;
    if (aiBehaviorSelectorModal == null) return;

    aiBehaviorSelectorModal.Show(gm.selectedPiece);
  }
}
