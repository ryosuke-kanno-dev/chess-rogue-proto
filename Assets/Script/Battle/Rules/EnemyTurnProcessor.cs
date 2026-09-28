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
                if (!rules.TryGetValue(enemy.enemyArchetype, out var rule) ||
                    !patterns.TryGetValue(enemy.enemyArchetype, out var pattern))
                {
                    continue;
                }

                // スタン中は、移動もキングへの攻撃もできない（案S1）
                if (HasActiveEffect(enemy, StatusEffectType.Stun)) continue;

                var finalPosition = enemy.position;

                // スロウ中は、付与ターンから数えて(1 + magnitude)ターンに1回しか移動できない（案L1）。
                // 移動を飛ばしても、既にキングに隣接していればキングへの攻撃は行う。
                if (!ShouldSkipMoveBySlow(enemy, board.CurrentTurn))
                {
                    var destination = rule.DetermineMoveDestination(enemy, board, pattern);
                    if (destination.HasValue)
                    {
                        board.ApplyMove(false, enemy.position, destination.Value);
                        finalPosition = destination.Value;
                    }
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

        private static bool HasActiveEffect(PieceSnapshot piece, StatusEffectType type)
        {
            foreach (var effect in piece.statusEffects)
            {
                if (effect.type == type && effect.remainingTurns > 0) return true;
            }
            return false;
        }

        // 付与ターン(appliedTurn)を起点に数えるため、付与直後の敵ターンは必ず移動を飛ばし、
        // 次のターンは動ける、という決定的な挙動になる（現在ターンの偶奇に依存しない）。
        private static bool ShouldSkipMoveBySlow(PieceSnapshot piece, int currentTurn)
        {
            foreach (var effect in piece.statusEffects)
            {
                if (effect.type != StatusEffectType.Slow || effect.remainingTurns <= 0) continue;

                int interval = 1 + Mathf.Max(0, Mathf.RoundToInt(effect.magnitude));
                if ((currentTurn - effect.appliedTurn) % interval == 0) return true;
            }
            return false;
        }
    }
}
