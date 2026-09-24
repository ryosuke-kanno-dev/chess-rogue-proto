using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ステップ18: インベントリの1スロット分の表示を担当するコンポーネント。
// スロットのプレハブ（Button + Image + TextMeshProUGUI を持つUI要素）にアタッチして使用する。
// InventoryUI.csがMaxInventorySlots個ぶんだけこのプレハブを起動時に1度だけ生成し、
// 以降は本コンポーネントのSetData/SetEmptyで表示内容だけを差し替える（Instantiateの繰り返しはしない）。
//
// 課題【左右クリック分岐】: 従来のButton.onClickベースの単一クリック動作から、
// IPointerClickHandlerを実装する形へ変更し、「左クリック＝詳細表示」「右クリック＝装着」の
// 2動作に分岐できるようにする。Buttonコンポーネント自体（slotButton）はクリック検知には使わず、
// 引き続き「interactable（空きスロット時にfalseにする等）」の見た目制御のためだけに残す。
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
  [Header("スロットUI参照")]
  [SerializeField] private Button slotButton;
  [SerializeField] private Image iconImage;          // レアリティに応じて色分けする背景/アイコン
  [SerializeField] private TextMeshProUGUI nameText;  // アイテム名
  [SerializeField] private GameObject emptyIndicator; // 空きスロット時にだけ表示したい装飾（任意・未設定可）

  private EquipmentInstance currentItem;
  private System.Action<EquipmentInstance> onLeftClick;
  private System.Action<EquipmentInstance> onRightClick;

  // InventoryUI側から一度だけ呼び出し、左クリック・右クリックそれぞれのコールバックを登録する
  public void Initialize(System.Action<EquipmentInstance> onLeftClickHandler, System.Action<EquipmentInstance> onRightClickHandler)
  {
    onLeftClick = onLeftClickHandler;
    onRightClick = onRightClickHandler;

    if (slotButton == null)
    {
      Debug.LogWarning($"⚠️ InventorySlotUI（{gameObject.name}）: Slot Button がInspectorで未設定です（見た目制御のinteractableが効かなくなります）。");
    }
  }

  // このスロットにアイテムを表示する
  public void SetData(EquipmentInstance item, Color rarityColor)
  {
    currentItem = item;

    if (slotButton != null) slotButton.interactable = true;
    if (iconImage != null) iconImage.color = rarityColor;
    if (nameText != null) nameText.text = item != null ? item.itemName : "";
    if (emptyIndicator != null) emptyIndicator.SetActive(false);
  }

  // このスロットを「空き」表示にする
  public void SetEmpty()
  {
    currentItem = null;

    if (slotButton != null) slotButton.interactable = false;
    if (iconImage != null) iconImage.color = new Color(1f, 1f, 1f, 0.15f);
    if (nameText != null) nameText.text = "";
    if (emptyIndicator != null) emptyIndicator.SetActive(true);
  }

  // 課題【左右クリック分岐】: クリック検知自体はButton.onClickではなく、こちらで一元的に行う
  public void OnPointerClick(PointerEventData eventData)
  {
    if (currentItem == null) return;

    if (eventData.button == PointerEventData.InputButton.Left)
    {
      onLeftClick?.Invoke(currentItem);
    }
    else if (eventData.button == PointerEventData.InputButton.Right)
    {
      onRightClick?.Invoke(currentItem);
    }
  }
}
