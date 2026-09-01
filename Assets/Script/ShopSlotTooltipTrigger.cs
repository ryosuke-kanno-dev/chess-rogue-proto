using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 課題【駒特性説明のホバーツールチップ・ショップ側】:
// ショップボタン(UGUI)にアタッチし、マウスホバーでTooltipUIに駒名+特性説明文を表示させる。
// こちらは通常のUGUIのポインターイベント(IPointerEnterHandler/IPointerExitHandler)で実装する
// （3D側のPieceTooltipTriggerとは異なり、UGUI標準のイベントで問題ない）。
//
// UIManager.UpdateShop() 側で、ショップの中身（PieceType）が変わるたび（購入・リロール等）に
// SetPieceType() を呼び直して、常に「今そのボタンに表示されている駒種」の説明文が出るようにする。
public class ShopSlotTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  private PieceType pieceType;
  private bool hasPieceType = false;

  // 課題【修正2: ショップ内容変更時にツールチップが更新されない問題】: 現在ホバー中かどうかを保持するフラグ。
  // カーソルがボタンに乗ったままショップの中身が変わった場合(リロール等)でも、
  // このフラグを見てSetPieceType側から表示内容を更新し直せるようにする。
  private bool isHovering = false;

  // UIManager.UpdateShop()から、ボタンごとの現在のPieceTypeを都度渡してもらうための公開メソッド
  public void SetPieceType(PieceType type)
  {
    bool changed = !hasPieceType || pieceType != type;
    pieceType = type;
    hasPieceType = true;

    // 課題【修正2】: ホバーし直さなくても、ホバー中に中身が変わった瞬間に表示を追従させる。
    // 「ホバー中」かつ「実際にPieceTypeが変わった」場合のみ再表示し、無関係な毎フレーム呼び出しでは何もしない
    // （UIManager.UpdateShop()は毎フレーム呼ばれるが、changedがfalseの間はここで弾かれるため負荷は増えない）。
    if (isHovering && changed)
    {
      ShowCurrentTooltip();
    }
  }

  public void OnPointerEnter(PointerEventData eventData)
  {
    isHovering = true;
    ShowCurrentTooltip();
  }

  public void OnPointerExit(PointerEventData eventData)
  {
    isHovering = false;
    if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
  }

  // 課題【データソース】: UnitStatusDataSOのabilityName/abilityDescriptionのみを渡す。
  // OnPointerEnter・SetPieceType（ホバー中の内容変化時）の両方から共通で呼ぶ。
  void ShowCurrentTooltip()
  {
    if (!hasPieceType) return;
    if (TooltipUI.Instance == null) return;
    if (DebugGameManager.Instance == null || DebugGameManager.Instance.UnitStatusData == null) return;
    if (Mouse.current == null) return;

    UnitStatusDataSO.UnitStatusEntry entry = DebugGameManager.Instance.UnitStatusData.GetStats(pieceType);
    if (entry == null) return;

    // このプロジェクトは新Input System統一のため、位置取得もMouse.current経由に揃える
    // （SetPieceType経由での再表示時はPointerEventDataが無いため、こちらが必要）
    Vector2 screenPos = Mouse.current.position.ReadValue();
    TooltipUI.Instance.Show(entry.abilityName, entry.abilityDescription, screenPos);
  }

  // ボタンが非活性化・非表示になった際にツールチップが残り続けないようにする保険
  void OnDisable()
  {
    isHovering = false;
    if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
  }
}
