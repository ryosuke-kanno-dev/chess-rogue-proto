using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Chebyss.Battle;

public class PlacementDraggable : MonoBehaviour
{
    private const float DragThresholdDistance = 14f;

    [Header("ドラッグ中の見た目の高さ（演出上の好みなのでInspectorで調整可能にする）")]
    [SerializeField] private float draggingHoverOffset = 0.35f;

    private PieceBoardLink pieceBoardLink;

    private Camera mainCamera;
    private PlacementController placementController;
    private BoardGenerator boardGenerator;
    private BattleVisualSync visualSync;
    private PieceType pieceType;

    private bool isDragging;
    private bool isPlaced;
    private Vector3 lastValidPosition;
    // 未配置（待機中）と「グリッド(0,0)に配置済み」を区別するためnullableにする
    private Vector2Int? lastValidGridPosition;
    private Vector3 dragOffset;

    // 配置時の範囲プレビュー
    private PlacementRangePreview rangePreview;
    private bool isShowingPreview; // 自分が表示させているプレビューだけを消すためのフラグ
    private static PlacementDraggable currentDragger; // 複数の駒のうち、今ドラッグ中のもの

    // クリック配置（PlacementInputController）との連携
    private PlacementInputController placementInput;
    private Vector2 pressMousePos;
    private bool wasBobbing;

    private const float SelectionBobAmplitude = 0.08f;
    private const float SelectionBobPeriodSeconds = 1.2f;

    // 他の駒（クリック配置のプレビュー等）が、ドラッグ中かどうかを知るための公開プロパティ
    public static bool IsAnyDragging => currentDragger != null;

    public void Initialize(PlacementController controller, BoardGenerator generator, BattleVisualSync sync, Vector3 stagingPosition, PlacementRangePreview preview = null, PlacementInputController input = null)
    {
        rangePreview = preview;
        placementInput = input;
        placementController = controller;
        boardGenerator = generator;
        visualSync = sync;
        pieceType = GetComponentInChildren<PieceData>(true).type;
        lastValidPosition = stagingPosition;
        lastValidGridPosition = null;
        isPlaced = false;

        pieceBoardLink = GetComponent<PieceBoardLink>();
        if (pieceBoardLink == null) pieceBoardLink = gameObject.AddComponent<PieceBoardLink>();

        pieceBoardLink.GroundedHeightOffset = PieceBoardLink.ComputeGroundedHeightOffset(transform);
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            // コライダーが子オブジェクト（VisualModel等）にある場合、hit.transformは
            // 子のtransformになりルートのtransformとは一致しないため、rootどうしで比較する
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform.root == transform.root)
            {
                isDragging = true;
                currentDragger = this;
                pressMousePos = mousePos;
                ClearPreviewIfMine(); // ホバー中のプレビューは、ドラッグ用のプレビューに切り替える

                if (isPlaced)
                {
                    placementController.RemovePlacedPiece(pieceType);
                    isPlaced = false;
                }

                Plane plane = new Plane(Vector3.up, transform.position);
                if (plane.Raycast(ray, out float enter))
                    dragOffset = transform.position - ray.GetPoint(enter);
            }
        }

        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 targetPos = ray.GetPoint(enter) + dragOffset;
                transform.position = new Vector3(targetPos.x, pieceBoardLink.GroundedHeightOffset + draggingHoverOffset, targetPos.z);
            }

            UpdateDragPreview(mousePos);
        }

        if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            ClearPreviewIfMine();
            if (currentDragger == this) currentDragger = null;

            // 押してから離すまでの移動量がしきい値未満なら「クリック」（選択の切り替え）、
            // それ以上なら「ドラッグ」（従来どおり配置を試みる）
            bool isClick = Vector2.Distance(pressMousePos, mousePos) < DragThresholdDistance;
            if (isClick)
            {
                RevertToLastValid();

                if (placementInput != null)
                {
                    if (placementInput.SelectedType == pieceType) placementInput.Deselect();
                    else placementInput.Select(pieceType);
                }
            }
            else
            {
                TryDropOnTile(mousePos);
                if (placementInput != null) placementInput.Deselect();
            }
        }

        if (!isDragging) UpdateHoverPreview(mousePos);

        UpdateSelectionBob();
    }

    void OnDestroy()
    {
        if (currentDragger == this) currentDragger = null;
        ClearPreviewIfMine();

        // 選択中の上下動のまま破棄される（バトル開始）場合に、高さを元へ戻す
        if (wasBobbing && !isDragging)
        {
            wasBobbing = false;
            transform.position = lastValidPosition;
        }
    }

    // 直前の有効な位置（配置済みならそのマス、未配置なら待機位置）へ戻す
    private void RevertToLastValid()
    {
        transform.position = lastValidPosition;
        if (lastValidGridPosition.HasValue)
        {
            var revert = placementController.PlacePendingPiece(pieceType, lastValidGridPosition.Value);
            isPlaced = revert == PlacementResult.Success;
        }
    }

    // 選択中の駒を、基準の高さからゆっくり上下に動かす。選択解除で元の高さに戻す。ドラッグ中は動かさない。
    private void UpdateSelectionBob()
    {
        bool shouldBob = !isDragging && placementInput != null && placementInput.SelectedType == pieceType;

        if (shouldBob)
        {
            float offset = Mathf.Sin(Time.time * (2f * Mathf.PI / SelectionBobPeriodSeconds)) * SelectionBobAmplitude;
            transform.position = new Vector3(lastValidPosition.x, lastValidPosition.y + offset, lastValidPosition.z);
            wasBobbing = true;
        }
        else if (wasBobbing)
        {
            wasBobbing = false;
            if (!isDragging) transform.position = lastValidPosition;
        }
    }

    // ---- 配置時の範囲プレビュー ----

    // ドラッグ中：カーソル下のマスが自陣エリア内ならそのマスでプレビューし、外なら消す
    private void UpdateDragPreview(Vector2 mousePos)
    {
        if (rangePreview == null) return;

        if (TryGetTileUnderCursor(mousePos, out var tile) && placementController.IsInDeploymentZone(tile.GridPosition))
        {
            rangePreview.ShowFor(pieceType, tile.GridPosition);
            isShowingPreview = true;
        }
        else
        {
            ClearPreviewIfMine();
        }
    }

    // ドラッグしていないとき：配置済みの駒の上にマウスがあれば、その駒の現在のマスでプレビューする
    private void UpdateHoverPreview(Vector2 mousePos)
    {
        if (rangePreview == null) return;

        bool hovering = false;
        if (isPlaced && lastValidGridPosition.HasValue && currentDragger == null &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            hovering = Physics.Raycast(ray, out RaycastHit hit) && hit.transform.root == transform.root;
        }

        if (hovering)
        {
            rangePreview.ShowFor(pieceType, lastValidGridPosition.Value);
            isShowingPreview = true;
        }
        else
        {
            ClearPreviewIfMine();
        }
    }

    // 複数の駒が同じプレビューを共有しているため、自分が表示させたときだけ消す
    private void ClearPreviewIfMine()
    {
        if (!isShowingPreview) return;
        isShowingPreview = false;
        if (rangePreview != null) rangePreview.Clear();
    }

    private bool TryGetTileUnderCursor(Vector2 mousePos, out BoardTile tile)
    {
        tile = null;
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        int boardTileLayerMask = LayerMask.GetMask("BoardTile");
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boardTileLayerMask)) return false;

        tile = hit.collider.GetComponent<BoardTile>();
        return tile != null;
    }

    // ---- 自動配置 ----

    // 指定マスへ配置する（ドロップ成功時と同じ処理）。配置済みなら置き直し、失敗したら元の位置へ戻す。
    public bool PlaceAt(Vector2Int gridPosition)
    {
        if (placementController == null || boardGenerator == null) return false;
        if (boardGenerator.GetTile(gridPosition) == null) return false;

        Vector2Int? previousGrid = isPlaced ? lastValidGridPosition : null;
        if (isPlaced)
        {
            placementController.RemovePlacedPiece(pieceType);
            isPlaced = false;
        }

        var result = placementController.PlacePendingPiece(pieceType, gridPosition);
        if (result == PlacementResult.Success)
        {
            FinalizePlacement(gridPosition);
            return true;
        }

        if (previousGrid.HasValue)
        {
            var revert = placementController.PlacePendingPiece(pieceType, previousGrid.Value);
            isPlaced = revert == PlacementResult.Success;
        }
        return false;
    }

    // PlacementControllerへの配置が成功した後の、見た目・状態の更新（ドロップ・PlaceAt共通）
    private void FinalizePlacement(Vector2Int gridPosition)
    {
        Vector3 tileWorldPos = boardGenerator.GetTile(gridPosition).transform.position;
        // タイル表面の高さ + このモデル固有のオフセットで、底面がぴったり接地する
        transform.position = new Vector3(tileWorldPos.x, tileWorldPos.y + pieceBoardLink.GroundedHeightOffset, tileWorldPos.z);
        lastValidPosition = transform.position;
        lastValidGridPosition = gridPosition;
        isPlaced = true;
        pieceBoardLink.GridPosition = gridPosition;
        // 実際に盤面へ配置できた時点で初めて登録する。
        // まだ配置されていない（BoardStateに存在しない）駒をSyncAllが
        // 「撃破された」と誤認してDestroyしてしまうバグがあったため、
        // 生成直後ではなく配置成功時に登録する。
        visualSync.Register(pieceBoardLink);
    }

    private void TryDropOnTile(Vector2 mousePos)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        BoardTile hitTile = null;

        // 駒自身のコライダーがマウス直下に浮いているため、通常のレイキャストだと
        // タイルより先に駒自身へ当たってしまう。BoardTile専用レイヤーに限定することで防ぐ。
        int boardTileLayerMask = LayerMask.GetMask("BoardTile");
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boardTileLayerMask))
            hitTile = hit.collider.GetComponent<BoardTile>();

        if (hitTile != null)
        {
            var result = placementController.PlacePendingPiece(pieceType, hitTile.GridPosition);
            if (result == PlacementResult.Success)
            {
                FinalizePlacement(hitTile.GridPosition);
                return;
            }

            Debug.Log($"[PlacementDraggable] {pieceType}の配置に失敗: {result}");
        }

        // 配置失敗・タイル外へのドロップ時は直前の有効な位置へ戻す
        transform.position = lastValidPosition;
        if (lastValidGridPosition.HasValue)
        {
            var revert = placementController.PlacePendingPiece(pieceType, lastValidGridPosition.Value);
            isPlaced = revert == PlacementResult.Success;
        }
    }
}
