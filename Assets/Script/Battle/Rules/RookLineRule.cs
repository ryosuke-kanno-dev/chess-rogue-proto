using System.Collections.Generic;
using UnityEngine; // Vector2Intのみ使用

namespace Chebyss.Battle
{
    /// <summary>
    /// ルークの縦横射線移動・ノックバック攻撃ルール。
    /// Phase1はenableChainCollision=false固定（LineMovementHelperの共通処理を使用）。
    /// </summary>
    public class RookLineRule : IMovementAttackRule
    {
        private static readonly Vector2Int[] Directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
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
            var rookPattern = (RookMovementPatternSO)pattern;

            // TODO: ルークのダメージに距離ボーナスを持たせるかは未確定。現状は基礎攻撃力そのまま。
            int damage = attacker.attack;
            bool defeated = target.currentHp - damage <= 0;

            Vector2Int? knockbackDestination = null;
            if (!defeated && rookPattern.knockbackDistance > 0)
            {
                var direction = ClampToUnitDirection(target.position - attacker.position);
                knockbackDestination = LineMovementHelper.FindPushDestination(
                    target.position, direction, rookPattern.knockbackDistance, board);
                // TODO: ノックバック距離のデフォルト値は未確定（design doc「未決定事項」参照）。
            }

            // Phase1: enableChainCollision=false固定。Phase2で玉突きダメージを実装する。
            ChainCollisionResult? chainResult = null;

            return new AttackOutcome(
                damage: damage,
                targetDefeated: defeated,
                appliedEffects: pattern.onHitEffects,
                knockbackDestination: knockbackDestination,
                pursuitMoveCandidates: null,
                chainCollisionResult: chainResult);
        }

        private static Vector2Int ClampToUnitDirection(Vector2Int delta) =>
            new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
    }
}
