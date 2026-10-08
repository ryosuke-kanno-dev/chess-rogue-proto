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
            var actions = new List<EnemyActionRecord>();

            foreach (var enemyPos in board.GetAllEnemyPiecePositionsOrdered())
            {
                if (!board.TryGetEnemyPiece(enemyPos, out var enemy)) continue;

                if (!rules.TryGetValue(enemy.enemyArchetype, out var rule) ||
                    !patterns.TryGetValue(enemy.enemyArchetype, out var pattern))
                {
                    continue;
                }

                if (HasActiveEffect(enemy, StatusEffectType.Stun))
                {
                    actions.Add(new EnemyActionRecord(enemy.instanceId, enemy.position, true, null, false));
                    continue;
                }

                var finalPosition = enemy.position;
                Vector2Int? destination = null;

                if (!ShouldSkipMoveBySlow(enemy, board.CurrentTurn))
                {
                    destination = rule.DetermineMoveDestination(enemy, board, pattern);
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
                    actions.Add(new EnemyActionRecord(enemy.instanceId, enemy.position, false, destination, true));

                    if (board.TryGetPlayerPiece(board.KingPosition, out var king) && king.currentHp <= 0)
                        return new EnemyTurnResult(kingDefeated: true, actions);

                    continue;
                }

                actions.Add(new EnemyActionRecord(enemy.instanceId, enemy.position, false, destination, false));
            }

            return new EnemyTurnResult(kingDefeated: false, actions);
        }

        // インテント表示用：BoardStateの複製の上でProcess()をそのまま実行し、
        // 実際に起きることと完全に一致する予告を得る。複製なので本物の盤面には影響しない。
        public EnemyTurnResult PreviewNextTurn(BoardState board)
        {
            return Process(board.Clone());
        }

        private static bool HasActiveEffect(PieceSnapshot piece, StatusEffectType type)
        {
            foreach (var effect in piece.statusEffects)
                if (effect.type == type && effect.remainingTurns > 0) return true;
            return false;
        }

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
