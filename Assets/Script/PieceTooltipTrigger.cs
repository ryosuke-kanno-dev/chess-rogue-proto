using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// 課題【駒特性説明のホバーツールチップ・盤面駒側】:
// 盤面上の駒(3Dオブジェクト)にアタッチし、マウスホバーでTooltipUIに駒名+特性説明文を表示させる。
//
// 【重要】このプロジェクトは新Input System(UnityEngine.InputSystem の Mouse.current)を使用しており、
// PieceDraggable.cs も OnMouseEnter/OnMouseDown 等の旧Input Manager系マジックメソッドは一切使わず、
// Update内で Mouse.current.position を取得して Physics.Raycast する自前実装になっている。
// このスクリプトも同じ方式（Update内でRaycastし、ヒット対象が自分かどうかで開始/終了を判定）で統一し、
// OnMouseEnter/OnMouseExitは使用しない（Input System設定によっては正しく動作しない可能性があるため）。
[RequireComponent(typeof(PieceData))]
public class PieceTooltipTrigger : MonoBehaviour
{
  private Camera mainCamera;
  private PieceData myData;
  private PieceDraggable myDraggable; // 同じ駒に付いていれば、ドラッグ中かどうかの判定に使う（無くても動作する）
  private bool isHovering = false;

  void Start()
  {
    mainCamera = Camera.main;
    myData = GetComponent<PieceData>();
    myDraggable = GetComponent<PieceDraggable>();
  }

  void Update()
  {
    if (mainCamera == null) mainCamera = Camera.main; // ステップ7相当: カメラ参照の防御的再取得
    if (mainCamera == null || Mouse.current == null || myData == null) return;

    // 課題【撃破済みの駒には出さない】
    if (myData.currentHp <= 0)
    {
      StopHoveringIfNeeded();
      return;
    }

    // 課題【ドラッグ中は表示しない】: PieceDraggable.IsDragging（isDraggingフィールドの公開プロパティ）を参照するだけで、
    // PieceDraggable側のドラッグ判定ロジック自体には一切手を加えない。
    if (myDraggable != null && myDraggable.IsDragging)
    {
      StopHoveringIfNeeded();
      return;
    }

    // 課題【UIの上ではRaycastしない】: PieceDraggable.cs側にある
    // 「EventSystem.current.IsPointerOverGameObject()の場合は3D側の処理をしない」というガードと同様のものを
    // ここでも入れ、ショップボタン等のUIの上をマウスが通過した際に盤面側のRaycastが誤反応しないようにする。
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
    {
      StopHoveringIfNeeded();
      return;
    }

    Vector2 mousePos = Mouse.current.position.ReadValue();
    Ray ray = mainCamera.ScreenPointToRay(mousePos);

    bool hitSelf = Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform;

    if (hitSelf)
    {
      // 課題【修正1: 毎フレームのテキスト再代入をやめる】:
      // 以前はホバーが継続している間、毎フレーム ShowTooltip() → TooltipUI.Show() を呼んでおり、
      // 実際には変化しないabilityName/abilityDescriptionまで毎フレーム再代入していた。
      // isHoveringが false→true に変わった瞬間（＝表示を開始する瞬間）だけテキストを設定するShow()を呼び、
      // 既に表示中（isHoveringがtrueのまま継続）のフレームでは、位置だけを更新するUpdatePosition()のみを呼ぶ。
      if (!isHovering)
      {
        isHovering = true;
        ShowTooltip(mousePos);
      }
      else if (TooltipUI.Instance != null)
      {
        TooltipUI.Instance.UpdatePosition(mousePos);
      }
    }
    else
    {
      StopHoveringIfNeeded();
    }
  }

  void ShowTooltip(Vector2 mousePos)
  {
    if (TooltipUI.Instance == null) return;
    if (DebugGameManager.Instance == null || DebugGameManager.Instance.UnitStatusData == null) return;

    // 課題【データソース】: UnitStatusDataSOのabilityName/abilityDescriptionのみを渡す
    // （HP/攻撃力等の数値はスコープ外のため一切参照しない）
    UnitStatusDataSO.UnitStatusEntry entry = DebugGameManager.Instance.UnitStatusData.GetStats(myData.type);
    if (entry == null) return;

    // 課題【★2→★3合成の育成履歴分岐システム】: ★3進化済み（evolvedVariantNameが空でない）の場合、
    // 既存の説明文に加えて進化後のフレーバー名+追加説明文を追記する（abilityName自体は変更しない）。
    string description = entry.abilityDescription;
    if (!string.IsNullOrEmpty(myData.evolvedVariantName))
    {
      description += $"\n★3進化: {myData.evolvedVariantName} - {myData.evolvedVariantDescription}";
    }

    TooltipUI.Instance.Show(entry.abilityName, description, mousePos);
  }

  // ホバーが外れた時、「自分が表示させていた場合に限り」ツールチップを隠す。
  // （他の駒のトリガーが同一フレームで新たにShow()した直後に、無関係なStopHoveringIfNeeded()が
  //   それを消してしまう、といった競合を避けるため、isHoveringフラグで自分の状態のみを管理する）
  void StopHoveringIfNeeded()
  {
    if (!isHovering) return;
    isHovering = false;
    if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
  }

  void OnDisable()
  {
    StopHoveringIfNeeded();
  }
}
