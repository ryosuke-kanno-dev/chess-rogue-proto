using System.Collections.Generic;
using UnityEngine; // Vector2Intのみ使用

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
            var dest = piece.position + forward;

            if (board.IsInBounds(dest) && !board.IsOccupied(dest))
                result.Add(dest);

            return result;
        }

        public List<Vector2Int> GetAttackableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var result = new List<Vector2Int>();
            var forward = ForwardOffset(piece.isPlayerSide);

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

        private static Vector2Int ForwardOffset(bool isPlayerSide) =>
            new Vector2Int(0, isPlayerSide ? 1 : -1);
    }
}
