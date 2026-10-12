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

            // 全滅判定用：このターンの開始時にキング以外の味方駒がいたか
            bool hadPieces = CountNonKingPlayerPieces(board) > 0;

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

                // a. キングに隣接していれば、従来どおりキングを攻撃する
                if (NearestPieceMoveHelper.ChebyshevDistance(finalPosition, board.KingPosition) == 1)
                {
                    int kingDamage = pattern.kingDamage;
                    var kingDamageOutcome = new AttackOutcome(
                        damage: kingDamage,
                        targetDefeated: false,
                        appliedEffects: null,
                        knockbackDestination: null,
                        pursuitMoveCandidates: null,
                        chainCollisionResult: null);

                    board.ApplyAttack(true, board.KingPosition, kingDamageOutcome);

                    bool kingDown = board.TryGetPlayerPiece(board.KingPosition, out var king) && king.currentHp <= 0;
                    actions.Add(new EnemyActionRecord(
                        enemy.instanceId, enemy.position, false, destination, true,
                        board.KingPosition, kingDamage, kingDown));

                    if (kingDown)
                        return new EnemyTurnResult(kingDefeated: true, actions);

                    continue;
                }

                // b. キングに隣接していない場合のみ、隣接する味方駒（キング除く）のうちHPが最も低い駒を攻撃する。
                // c. 対象は毎回その時点の盤面から選ぶため、同ターン内で駒が倒れた後は残りの駒から選び直される。
                if (TryFindWeakestAdjacentPlayerPiece(board, finalPosition, out var targetPos, out var target))
                {
                    int damage = enemy.attack;
                    bool defeated = target.currentHp - damage <= 0;

                    var outcome = new AttackOutcome(
                        damage: damage,
                        targetDefeated: defeated,
                        appliedEffects: null,
                        knockbackDestination: null,
                        pursuitMoveCandidates: null,
                        chainCollisionResult: null);

                    board.ApplyAttack(true, targetPos, outcome);

                    actions.Add(new EnemyActionRecord(
                        enemy.instanceId, enemy.position, false, destination, false,
                        targetPos, damage, defeated));
                    continue;
                }

                actions.Add(new EnemyActionRecord(enemy.instanceId, enemy.position, false, destination, false));
            }

            bool allPiecesDefeated = hadPieces && CountNonKingPlayerPieces(board) == 0;
            return new EnemyTurnResult(kingDefeated: false, actions, allPiecesDefeated);
        }

        // インテント表示用：BoardStateの複製の上でProcess()をそのまま実行し、
        // 実際に起きることと完全に一致する予告を得る。複製なので本物の盤面には影響しない。
        public EnemyTurnResult PreviewNextTurn(BoardState board)
        {
            return Process(board.Clone());
        }

        private static int CountNonKingPlayerPieces(BoardState board)
        {
            int count = 0;
            foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
            {
                if (board.TryGetPlayerPiece(pos, out var piece) && piece.pieceType != PieceType.King)
                    count++;
            }
            return count;
        }

        // 隣接（チェビシェフ距離1）する味方駒（キング除く）のうちcurrentHpが最小の駒。
        // 同値は GetAllPlayerPiecePositionsOrdered の順で先のものを優先する（厳密に小さい場合のみ更新）。
        private static bool TryFindWeakestAdjacentPlayerPiece(
            BoardState board, Vector2Int from, out Vector2Int targetPos, out PieceSnapshot target)
        {
            targetPos = default;
            target = default;
            bool found = false;

            foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
            {
                if (!board.TryGetPlayerPiece(pos, out var piece)) continue;
                if (piece.pieceType == PieceType.King) continue;
                if (NearestPieceMoveHelper.ChebyshevDistance(from, pos) != 1) continue;

                if (!found || piece.currentHp < target.currentHp)
                {
                    found = true;
                    targetPos = pos;
                    target = piece;
                }
            }

            return found;
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
