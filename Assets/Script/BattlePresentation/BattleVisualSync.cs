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

    public void Initialize(BoardGenerator generator, StatusIndicatorSettings settings)
    {
        boardGenerator = generator;
        statusSettings = settings;
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
            }

            var indicator = link.GetComponent<PieceStatusIndicator>();
            if (indicator == null)
            {
                indicator = link.gameObject.AddComponent<PieceStatusIndicator>();
                indicator.Initialize(statusSettings);
            }
            indicator.SetEffects(snapshot.statusEffects);
        }
    }
}
