using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// PieceSnapshotを構築する唯一の経路。PieceData.CreateSnapshot()は
    /// 必ずこのファクトリ経由でPieceSnapshotを作ること。
    /// ActiveStatusEffectのディープコピーと、statusEffectsが常にList&lt;ActiveStatusEffect&gt;
    /// の実体になることをここで保証する。
    /// </summary>
    public static class PieceSnapshotFactory
    {
        // 通常は1回。"BonusActions"（例："疾風の足"アイテム）があればその分加算する。
        // BoardState.ResetPlayerActionsLeftからも参照する共通ロジック。
        public static int ComputeMaxActions(IReadOnlyDictionary<string, float> statModifiers)
        {
            int bonus = statModifiers != null && statModifiers.TryGetValue("BonusActions", out var b) ? (int)b : 0;
            return 1 + bonus;
        }

        public static PieceSnapshot Create(
            PieceType pieceType,
            bool isPlayerSide,
            Vector2Int position,
            int currentHp,
            int attack,
            IReadOnlyDictionary<string, float> statModifiers,
            IEnumerable<ActiveStatusEffect> statusEffects,
            EnemyArchetype enemyArchetype = default)
        {
            var effectsCopy = new List<ActiveStatusEffect>(
                (statusEffects ?? Enumerable.Empty<ActiveStatusEffect>())
                    .Select(e => new ActiveStatusEffect
                    {
                        type = e.type,
                        remainingTurns = e.remainingTurns,
                        magnitude = e.magnitude
                    }));

            var resolvedStatModifiers = statModifiers ?? new Dictionary<string, float>();

            return new PieceSnapshot
            {
                pieceType = pieceType,
                isPlayerSide = isPlayerSide,
                position = position,
                currentHp = currentHp,
                attack = attack,
                statModifiers = resolvedStatModifiers,
                statusEffects = effectsCopy,
                actionsLeft = isPlayerSide ? ComputeMaxActions(resolvedStatModifiers) : 0,
                enemyArchetype = enemyArchetype
            };
        }
    }
}
