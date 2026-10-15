using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Chebyss.Battle;

// 配置フェーズのクリック操作：駒を選択してから、置きたいマスをクリックして配置する。
// ドラッグ配置（PlacementDraggable）と併用できる。配置フェーズ中だけ動作する。
public class PlacementInputController : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private BoardGenerator boardGenerator; // Initializeの署名上受け取る。マス解決はBoardTile.GridPositionで足りる
    private PlacementRangePreview preview;
    private Camera mainCamera;

    // 自分が表示させたプレビューだけを消すためのフラグ
    private bool isShowingPreview;

    public PieceType? SelectedType { get; private set; }

    public void Initialize(BattleBootstrapper bootstrapper, BoardGenerator generator, PlacementRangePreview preview)
    {
        this.bootstrapper = bootstrapper;
        this.boardGenerator = generator;
        this.preview = preview;
    }

    public void Select(PieceType type)
    {
        SelectedType = type;
    }

    public void Deselect()
    {
        SelectedType = null;
        ClearOwnPreview();
    }

    void Update()
    {
        // 設定画面を開いている間は、配置の操作を行わない
        if (BattleSettingsPanelUI.IsOpen)
        {
            ClearOwnPreview();
            return;
        }

        if (bootstrapper == null || !bootstrapper.IsPlacementPhase)
        {
            // バトル開始後は選択を残さない
            if (SelectedType.HasValue) Deselect();
            else ClearOwnPreview();
            return;
        }

        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        var mouse = Mouse.current;
        Vector2 mousePos = mouse.position.ReadValue();
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (mouse.rightButton.wasPressedThisFrame && SelectedType.HasValue)
            Deselect();

        // 駒の上のクリックは、駒側（PlacementDraggable）が選択の切り替えとして処理する
        bool overPiece = IsOverPlacementPiece(mousePos);

        if (mouse.leftButton.wasPressedThisFrame && !overUI && !overPiece)
            HandleLeftClick(mousePos);

        UpdatePreview(mousePos, overUI, overPiece);
    }

    void OnDisable()
    {
        ClearOwnPreview();
    }

    private void HandleLeftClick(Vector2 mousePos)
    {
        if (!SelectedType.HasValue) return;

        if (TryGetCell(mousePos, out var cell) && bootstrapper.Placement.IsInDeploymentZone(cell))
        {
            // 自陣エリア内でも、他の駒がいるマスには置けない（選択は維持する）
            if (bootstrapper.Board.IsOccupied(cell)) return;

            if (bootstrapper.TryGetDraggable(SelectedType.Value, out var draggable) && draggable.PlaceAt(cell))
                Deselect();
            return;
        }

        // 自陣エリア外・マス外のクリックは、選択を解除する
        Deselect();
    }

    // 駒が選択されていて、ドラッグ中でないとき、カーソル下の自陣マスで範囲をプレビューする
    private void UpdatePreview(Vector2 mousePos, bool overUI, bool overPiece)
    {
        // キーボードカーソルが有効な間は、キーボード側（KeyboardCursorController）がプレビューを出す
        var keyboardCursor = bootstrapper.KeyboardCursor;
        if (keyboardCursor != null && keyboardCursor.IsActive)
        {
            ClearOwnPreview();
            return;
        }

        if (!SelectedType.HasValue || preview == null || overUI || overPiece || PlacementDraggable.IsAnyDragging)
        {
            ClearOwnPreview();
            return;
        }

        if (TryGetCell(mousePos, out var cell) && bootstrapper.Placement.IsInDeploymentZone(cell))
        {
            preview.ShowFor(SelectedType.Value, cell);
            isShowingPreview = true;
        }
        else
        {
            ClearOwnPreview();
        }
    }

    private void ClearOwnPreview()
    {
        if (!isShowingPreview) return;
        isShowingPreview = false;
        if (preview != null) preview.Clear();
    }

    private bool IsOverPlacementPiece(Vector2 mousePos)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        return Physics.Raycast(ray, out RaycastHit hit) &&
               hit.collider.GetComponentInParent<PlacementDraggable>() != null;
    }

    // BoardTile専用レイヤーのレイキャストで、カーソル下のマスを解決する
    private bool TryGetCell(Vector2 mousePos, out Vector2Int cell)
    {
        cell = default;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        int boardTileLayerMask = LayerMask.GetMask("BoardTile");
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boardTileLayerMask)) return false;

        var tile = hit.collider.GetComponent<BoardTile>();
        if (tile == null) return false;

        cell = tile.GridPosition;
        return true;
    }
}
