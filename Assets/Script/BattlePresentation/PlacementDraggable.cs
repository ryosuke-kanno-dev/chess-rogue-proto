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

    public void Initialize(PlacementController controller, BoardGenerator generator, BattleVisualSync sync, Vector3 stagingPosition)
    {
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
        }

        if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            TryDropOnTile(mousePos);
        }
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
                Vector3 tileWorldPos = boardGenerator.GetTile(hitTile.GridPosition).transform.position;
                // タイル表面の高さ + このモデル固有のオフセットで、底面がぴったり接地する
                transform.position = new Vector3(tileWorldPos.x, tileWorldPos.y + pieceBoardLink.GroundedHeightOffset, tileWorldPos.z);
                lastValidPosition = transform.position;
                lastValidGridPosition = hitTile.GridPosition;
                isPlaced = true;
                pieceBoardLink.GridPosition = hitTile.GridPosition;
                // 実際に盤面へ配置できた時点で初めて登録する。
                // まだ配置されていない（BoardStateに存在しない）駒をSyncAllが
                // 「撃破された」と誤認してDestroyしてしまうバグがあったため、
                // 生成直後ではなく配置成功時に登録するタイミングへ変更した。
                visualSync.Register(pieceBoardLink);
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
