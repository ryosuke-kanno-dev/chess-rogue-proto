using UnityEngine;

public class PieceBoardLink : MonoBehaviour
{
    public Vector2Int GridPosition { get; set; }
    public float GroundedHeightOffset { get; set; }
    // BoardState上のPieceSnapshot.instanceIdと対応する。BattleVisualSyncが
    // このIDでBoardState側の最新状態を検索する。
    public int InstanceId { get; set; }

    // 駒の底面をタイル表面に合わせるためのオフセットを、Rendererの実際の境界から計算する。
    // PlacementDraggable・BattleBootstrapper（キング/敵の見た目生成）の両方から使う共通処理。
    public static float ComputeGroundedHeightOffset(Transform root)
    {
        var renderer = root.GetComponentInChildren<Renderer>();
        return root.position.y - renderer.bounds.min.y;
    }
}
