using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// ルーク（縦横）・ビショップ（斜め）で共通する
    /// 「1方向へ射線を列挙する」処理を切り出した静的クラス。
    /// </summary>
    public static class LineMovementHelper
    {
        public readonly struct LineWalkResult
        {
            public readonly IReadOnlyList<Vector2Int> movableTiles;
            public readonly Vector2Int? attackableEnemyTile;

            public LineWalkResult(IReadOnlyList<Vector2Int> movableTiles, Vector2Int? attackableEnemyTile)
            {
                this.movableTiles = movableTiles;
                this.attackableEnemyTile = attackableEnemyTile;
            }
        }

        /// <summary>
        /// originから指定方向へ射線を伸ばし、空マスを移動可能として列挙する。
        /// 自軍の駒に当たればそこで射線終了（移動先・攻撃対象どちらにもならない）。
        /// 相手の駒に当たればそのマスを攻撃対象として記録し、射線はそこで止まる。
        /// </summary>
        public static LineWalkResult WalkDirection(
            Vector2Int origin,
            Vector2Int direction,
            int maxRange, // 0 = 無制限
            bool originIsPlayerSide,
            BoardState board)
        {
            var movable = new List<Vector2Int>();
            Vector2Int? attackable = null;
            var current = origin;
            var steps = 0;

            while (true)
            {
                current += direction;
                steps++;

                if (!board.IsInBounds(current)) break;
                if (maxRange > 0 && steps > maxRange) break;

                bool hasOwnPiece = originIsPlayerSide
                    ? board.TryGetPlayerPiece(current, out _)
                    : board.TryGetEnemyPiece(current, out _);
                if (hasOwnPiece) break;

                bool hasOpponentPiece = originIsPlayerSide
                    ? board.TryGetEnemyPiece(current, out _)
                    : board.TryGetPlayerPiece(current, out _);
                if (hasOpponentPiece)
                {
                    attackable = current;
                    break;
                }

                movable.Add(current);
            }

            return new LineWalkResult(movable, attackable);
        }

        /// <summary>
        /// ノックバック等で「指定方向へ最大maxDistanceマスまで、障害物の手前で止まる
        /// 最終到達マス」を求める。1マスも進めない場合はnull（呼び出し側でノックバック
        /// 不発として扱う想定）。
        /// </summary>
        public static Vector2Int? FindPushDestination(
            Vector2Int origin,
            Vector2Int direction,
            int maxDistance,
            BoardState board)
        {
            Vector2Int? last = null;
            var current = origin;

            for (int i = 0; i < maxDistance; i++)
            {
                var next = current + direction;
                if (!board.IsInBounds(next)) break;
                if (board.IsOccupied(next)) break;

                last = next;
                current = next;
            }

            return last;
        }
    }
}
