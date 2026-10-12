using System.Collections.Generic;
using UnityEngine; // Vector2Int・Mathf

namespace Chebyss.Battle
{
    // ポーンの前進移動・斜め前攻撃ルール。移動先は空きマスのみ、攻撃先は敵がいる
    // マスのみで重複しない（RookLineRule・BishopLineRuleと同じ原則）。
    // 前方向はisPlayerSideで決まる（プレイヤー駒はy+1方向、敵側駒として将来
    // 使われる場合はy-1方向）。「二歩」（初手2マス前進）は採用していない。
    public class PawnAdvanceRule : IMovementAttackRule
    {
        public List<Vector2Int> GetMovableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var result = new List<Vector2Int>();
            var forward = ForwardOffset(piece.isPlayerSide);

            // ★2以降：前・左・右へ最大star2MoveRangeマス（途中に駒があればそこで止まる）。後退はしない。
            if (piece.rank >= 2)
            {
                int moveRange = GetStar2MoveRange(pattern);
                foreach (var dir in new[] { forward, new Vector2Int(-1, 0), new Vector2Int(1, 0) })
                {
                    var walk = LineMovementHelper.WalkDirection(piece.position, dir, moveRange, piece.isPlayerSide, board);
                    result.AddRange(walk.movableTiles);
                }
                return result;
            }

            var dest = piece.position + forward;

            if (board.IsInBounds(dest) && !board.IsOccupied(dest))
                result.Add(dest);

            return result;
        }

        public List<Vector2Int> GetAttackableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var result = new List<Vector2Int>();
            var forward = ForwardOffset(piece.isPlayerSide);

            // ★2以降：前・斜め前左・斜め前右の3方向へ最大star2AttackRangeマス。射線上で最初に当たる敵を攻撃できる
            // （途中に自軍・他の駒があれば遮られる）。
            if (piece.rank >= 2)
            {
                int attackRange = GetStar2AttackRange(pattern);
                foreach (var dir in new[] { forward, forward + new Vector2Int(-1, 0), forward + new Vector2Int(1, 0) })
                {
                    var walk = LineMovementHelper.WalkDirection(piece.position, dir, attackRange, piece.isPlayerSide, board);
                    if (walk.attackableEnemyTile.HasValue)
                        result.Add(walk.attackableEnemyTile.Value);
                }
                return result;
            }

            foreach (var sideOffset in new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0) })
            {
                var dest = piece.position + forward + sideOffset;
                if (!board.IsInBounds(dest)) continue;

                bool hasOpponent = piece.isPlayerSide
                    ? board.TryGetEnemyPiece(dest, out _)
                    : board.TryGetPlayerPiece(dest, out _);
                if (hasOpponent) result.Add(dest);
            }

            return result;
        }

        public AttackOutcome ExecuteAttack(PieceSnapshot attacker, PieceSnapshot target, BoardState board, PieceMovementPatternSO pattern)
        {
            int damage = attacker.attack;
            bool defeated = target.currentHp - damage <= 0;

            return new AttackOutcome(
                damage: damage,
                targetDefeated: defeated,
                appliedEffects: pattern.onHitEffects,
                knockbackDestination: null,
                pursuitMoveCandidates: null,
                chainCollisionResult: null);
        }

        private static int GetStar2MoveRange(PieceMovementPatternSO pattern) =>
            pattern is PawnMovementPatternSO pawnPattern ? Mathf.Max(1, pawnPattern.star2MoveRange) : 2;

        private static int GetStar2AttackRange(PieceMovementPatternSO pattern) =>
            pattern is PawnMovementPatternSO pawnPattern ? Mathf.Max(1, pawnPattern.star2AttackRange) : 2;

        private static Vector2Int ForwardOffset(bool isPlayerSide) =>
            new Vector2Int(0, isPlayerSide ? 1 : -1);
    }
}
