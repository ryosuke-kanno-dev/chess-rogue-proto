using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    public class EnemyTurnProcessor
    {
        private readonly IReadOnlyDictionary<EnemyArchetype, IEnemyMoveRule> rules;
        private readonly IReadOnlyDictionary<EnemyArchetype, EnemyMovementPatternSO> patterns;

        public EnemyTurnProcessor(
            IReadOnlyDictionary<EnemyArchetype, IEnemyMoveRule> rules,
            IReadOnlyDictionary<EnemyArchetype, EnemyMovementPatternSO> patterns)
        {
            this.rules = rules;
            this.patterns = patterns;
        }

        public EnemyTurnResult Process(BoardState board)
        {
            foreach (var enemyPos in board.GetAllEnemyPiecePositionsOrdered())
            {
                if (!board.TryGetEnemyPiece(enemyPos, out var enemy)) continue;

                // 未登録のアーキタイプはクラッシュではなく「何もしない」で安全側に倒す
                // （BattleTurnControllerのTryGetValue対応と同じ理由）
                if (!rules.TryGetValue(enemy.enemyArchetype, out var rule) ||
                    !patterns.TryGetValue(enemy.enemyArchetype, out var pattern))
                {
                    continue;
                }

                var destination = rule.DetermineMoveDestination(enemy, board, pattern);
                var finalPosition = enemy.position;

                if (destination.HasValue)
                {
                    board.ApplyMove(false, enemy.position, destination.Value);
                    finalPosition = destination.Value;
                }

                if (NearestPieceMoveHelper.ChebyshevDistance(finalPosition, board.KingPosition) == 1)
                {
                    var kingDamageOutcome = new AttackOutcome(
                        damage: pattern.kingDamage,
                        targetDefeated: false,
                        appliedEffects: null,
                        knockbackDestination: null,
                        pursuitMoveCandidates: null,
                        chainCollisionResult: null);

                    board.ApplyAttack(true, board.KingPosition, kingDamageOutcome);

                    if (board.TryGetPlayerPiece(board.KingPosition, out var king) && king.currentHp <= 0)
                        return new EnemyTurnResult(kingDefeated: true);
                }
            }

            return new EnemyTurnResult(kingDefeated: false);
        }
    }
}
