using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

// BoardState（唯一の真実）の状態を、実際の3DモデルのGameObjectへ反映する同期役。
// 位置・HP（PieceHealthBar経由の表示）を同期する。BoardState上に見つからなくなった
// 駒（撃破された駒）はGameObjectごと削除する（本格的な撃破演出は別途拡張する）。
public class BattleVisualSync : MonoBehaviour
{
    private BoardGenerator boardGenerator;
    private StatusIndicatorSettings statusSettings;
    private readonly List<PieceBoardLink> trackedLinks = new();
    private readonly Dictionary<int, BoardDropMarker> dropMarkers = new();

    // 鑑定レベルを返す関数（ドロップの円盤を、鑑定Lv1以上のときだけ表示するために使う）
    private System.Func<int> appraisalLevelProvider;

    public void Initialize(BoardGenerator generator, StatusIndicatorSettings settings, System.Func<int> appraisalLevelProvider = null)
    {
        boardGenerator = generator;
        statusSettings = settings;
        this.appraisalLevelProvider = appraisalLevelProvider;
    }

    public void Register(PieceBoardLink link)
    {
        if (!trackedLinks.Contains(link)) trackedLinks.Add(link);
    }

    public void SyncAll(BoardState board)
    {
        for (int i = trackedLinks.Count - 1; i >= 0; i--)
        {
            var link = trackedLinks[i];
            if (link == null)
            {
                trackedLinks.RemoveAt(i);
                continue;
            }

            if (!board.TryFindPieceById(link.InstanceId, out var snapshot, out _, out var position))
            {
                // BoardState上から消えている＝撃破された
                trackedLinks.RemoveAt(i);
                Destroy(link.gameObject);
                continue;
            }

            link.GridPosition = position;

            var tile = boardGenerator.GetTile(position);
            if (tile != null)
            {
                var tileWorldPos = tile.transform.position;
                link.transform.position = new Vector3(tileWorldPos.x, tileWorldPos.y + link.GroundedHeightOffset, tileWorldPos.z);
            }

            var pieceData = link.GetComponentInChildren<PieceData>(true);
            if (pieceData != null)
            {
                pieceData.maxHp = snapshot.maxHp;
                pieceData.currentHp = snapshot.currentHp;
                pieceData.attack = snapshot.attack;

                // rankの変化（進化）を検知したときだけ、旧EvolveToStar2()と同じ
                // 見た目の拡大演出を行う。ステータス自体はBoardState側で既に
                // 変化済みなので、ここではPieceData.EvolveToStar2()は呼ばない。
                if (pieceData.rank != snapshot.rank)
                {
                    if (snapshot.rank > pieceData.rank)
                        link.transform.localScale *= 1.15f;
                    pieceData.rank = snapshot.rank;
                }
            }

            var indicator = link.GetComponent<PieceStatusIndicator>();
            if (indicator == null)
            {
                indicator = link.gameObject.AddComponent<PieceStatusIndicator>();
                indicator.Initialize(statusSettings);
            }
            indicator.SetEffects(snapshot.statusEffects);

            // 敵駒にはactionsLeftという概念が無い（常に0）ため、プレイヤー駒にのみ適用する
            if (snapshot.isPlayerSide)
            {
                var actionState = link.GetComponent<PieceActionStateIndicator>();
                if (actionState == null) actionState = link.gameObject.AddComponent<PieceActionStateIndicator>();
                actionState.SetActed(snapshot.actionsLeft <= 0);
            }
        }

        SyncDrops(board);
    }

    // BoardStateのドロップに合わせてマーカーを生成し、BoardStateから消えたドロップのマーカーを破棄する。
    private void SyncDrops(BoardState board)
    {
        var current = board.GetAllDropsOrdered();
        var aliveIds = new HashSet<int>();

        foreach (var drop in current)
        {
            aliveIds.Add(drop.dropId);
            if (dropMarkers.ContainsKey(drop.dropId)) continue;

            var tile = boardGenerator.GetTile(drop.position);
            if (tile == null) continue;

            var markerObject = new GameObject($"DropMarker_{drop.dropId}");
            var marker = markerObject.AddComponent<BoardDropMarker>();
            bool showBoostDisc = appraisalLevelProvider != null && appraisalLevelProvider() >= 1;
            marker.Initialize(drop.dropId, drop.item, drop.boostedItem, showBoostDisc, tile.transform.position + Vector3.up * 0.45f);
            dropMarkers[drop.dropId] = marker;
        }

        var staleIds = new List<int>();
        foreach (var kvp in dropMarkers)
        {
            if (kvp.Value == null || !aliveIds.Contains(kvp.Key))
                staleIds.Add(kvp.Key);
        }

        foreach (var id in staleIds)
        {
            if (dropMarkers[id] != null) Destroy(dropMarkers[id].gameObject);
            dropMarkers.Remove(id);
        }
    }
}
