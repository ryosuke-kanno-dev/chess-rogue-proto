using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    public class EnemyTurnProcessor
    {
        private readonly IReadOnlyDictionary<PieceType, IEnemyMoveRule> rules;
        private readonly IReadOnlyDictionary<PieceType, EnemyMovementPatternSO> patterns;

        public EnemyTurnProcessor(
            IReadOnlyDictionary<PieceType, IEnemyMoveRule> rules,
            IReadOnlyDictionary<PieceType, EnemyMovementPatternSO> patterns)
        {
            this.rules = rules;
            this.patterns = patterns;
        }

        public EnemyTurnResult Process(BoardState board)
        {
            foreach (var enemyPos in board.GetAllEnemyPiecePositionsOrdered())
            {
                if (!board.TryGetEnemyPiece(enemyPos, out var enemy)) continue;

                var pattern = patterns[enemy.pieceType];
                var rule = rules[enemy.pieceType];
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
                        targetDefeated: false, // キングは墓地対象外のため常にfalse
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
