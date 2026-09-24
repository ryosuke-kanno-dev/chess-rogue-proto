using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    public static class NearestPieceMoveHelper
    {
        private static readonly Vector2Int[] EightDirections =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1)
        };

        public static Vector2Int? FindNearestPlayerPiece(Vector2Int from, BoardState board)
        {
            Vector2Int? nearest = null;
            int bestDistance = int.MaxValue;

            foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
            {
                int dist = ChebyshevDistance(from, pos);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    nearest = pos;
                }
            }

            return nearest;
        }

        public static Vector2Int? BfsFindNextStep(Vector2Int from, Vector2Int target, BoardState board)
        {
            // 既に隣接している（距離1以下）場合はそれ以上近づけない・近づく必要もないため、
            // BFSを実行せずnullを返す。
            // 修正前は「fromとtargetが完全に同じ座標」しか弾いておらず、
            // 隣接している場合にBFSがtarget（プレイヤー駒のマス）自体を
            // 「次の一歩」として返してしまい、敵駒がプレイヤー駒と同じ座標に
            // 移動する重大なバグがあった。
            if (ChebyshevDistance(from, target) <= 1) return null;

            var visited = new HashSet<Vector2Int> { from };
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var dir in EightDirections)
                {
                    var next = current + dir;
                    if (visited.Contains(next)) continue;
                    if (!board.IsInBounds(next)) continue;

                    bool isGoal = next == target;
                    if (!isGoal && board.IsOccupied(next)) continue;

                    visited.Add(next);
                    cameFrom[next] = current;

                    if (isGoal)
                        return ReconstructFirstStep(cameFrom, from, next);

                    queue.Enqueue(next);
                }
            }

            return null;
        }

        public static Vector2Int? FindFarthestAdjacentTile(Vector2Int from, Vector2Int nearestPlayerPos, BoardState board)
        {
            Vector2Int? best = null;
            int bestDistance = -1;

            foreach (var dir in EightDirections)
            {
                var candidate = from + dir;
                if (!board.IsInBounds(candidate)) continue;
                if (board.IsOccupied(candidate)) continue;

                int dist = ChebyshevDistance(candidate, nearestPlayerPos);
                if (dist > bestDistance)
                {
                    bestDistance = dist;
                    best = candidate;
                }
            }

            return best;
        }

        private static Vector2Int ReconstructFirstStep(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int from, Vector2Int goal)
        {
            var current = goal;
            while (cameFrom[current] != from)
                current = cameFrom[current];
            return current;
        }

        // privateではなくinternal static（EnemyTurnProcessorから参照するため）
        internal static int ChebyshevDistance(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
