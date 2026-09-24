using System.Collections.Generic;
using UnityEngine; // Vector2Intのみ使用

namespace Chebyss.Battle
{
    /// <summary>
    /// ビショップの斜め射線移動・スロウ付与攻撃ルール。
    /// 「低火力・広射程・スロウ付与による持続的な射線支配」として再定義済み
    /// （旧「低火力・高頻度」の記述は廃止）。ターン制の例外は作らない。
    /// </summary>
    public class BishopLineRule : IMovementAttackRule
    {
        private static readonly Vector2Int[] Directions =
        {
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        public List<Vector2Int> GetMovableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var result = new List<Vector2Int>();
            foreach (var dir in Directions)
            {
                var walk = LineMovementHelper.WalkDirection(piece.position, dir, pattern.moveRange, piece.isPlayerSide, board);
                result.AddRange(walk.movableTiles);
            }
            return result;
        }

        public List<Vector2Int> GetAttackableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var result = new List<Vector2Int>();
            foreach (var dir in Directions)
            {
                var walk = LineMovementHelper.WalkDirection(piece.position, dir, pattern.moveRange, piece.isPlayerSide, board);
                if (walk.attackableEnemyTile.HasValue)
                    result.Add(walk.attackableEnemyTile.Value);
            }
            return result;
        }

        public AttackOutcome ExecuteAttack(PieceSnapshot attacker, PieceSnapshot target, BoardState board, PieceMovementPatternSO pattern)
        {
            // TODO: ビショップの基礎ダメージ倍率（「低火力」の具体的な数値）は未確定。
            int damage = attacker.attack;
            bool defeated = target.currentHp - damage <= 0;

            return new AttackOutcome(
                damage: damage,
                targetDefeated: defeated,
                appliedEffects: pattern.onHitEffects, // Slowを含む定義をそのまま使う
                knockbackDestination: null,
                pursuitMoveCandidates: null,
                chainCollisionResult: null);
            // TODO: スロウmagnitudeのデフォルト値は未確定（design doc「未決定事項」参照）。
        }
    }
}
