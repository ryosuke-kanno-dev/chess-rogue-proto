using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ステップ19: 駒のインスペクトパネル内、装備欄1枠分の表示を担当するコンポーネント。
// InventorySlotUIと似た構成だが、こちらは右クリックで「装着」ではなく「取り外し（インベントリへ戻す）」を行う。
//
// 課題【左右クリック分岐】: 従来のButton.onClickベースの単一クリック動作から、
// IPointerClickHandlerを実装する形へ変更し、「左クリック＝詳細表示」「右クリック＝取り外し」の
// 2動作に分岐できるようにする。Buttonコンポーネント自体（unequipButton）はクリック検知には使わず、
// 見た目制御のためだけに残す。
public class EquippedItemSlotUI : MonoBehaviour, IPointerClickHandler
{
  [Header("スロットUI参照")]
  [SerializeField] private Button unequipButton;
  [SerializeField] private Image iconImage;          // レアリティに応じて色分けする背景/アイコン
  [SerializeField] private TextMeshProUGUI nameText;  // アイテム名
  [SerializeField] private TextMeshProUGUI statText;  // ステータス内訳（例: ATK+12 HP+40）

  private EquipmentInstance currentItem;
  private System.Action<EquipmentInstance> onLeftClick;
  private System.Action<EquipmentInstance> onRightClick;

  // PieceInspectPanelUI側から一度だけ呼び出し、左クリック・右クリックそれぞれのコールバックを登録する
  public void Initialize(System.Action<EquipmentInstance> onLeftClickHandler, System.Action<EquipmentInstance> onRightClickHandler)
  {
    onLeftClick = onLeftClickHandler;
    onRightClick = onRightClickHandler;
  }

  // この枠に装備を表示する
  public void SetData(EquipmentInstance item, Color rarityColor, string statLine)
  {
    currentItem = item;
    gameObject.SetActive(true);

    if (iconImage != null) iconImage.color = rarityColor;
    if (nameText != null) nameText.text = item != null ? item.itemName : "";
    if (statText != null) statText.text = statLine;
  }

  // 装備が入っていない枠は非表示にする
  public void Hide()
  {
    currentItem = null;
    gameObject.SetActive(false);
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
