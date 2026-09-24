using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// IMovementAttackRule.ExecuteAttackの戻り値。演出・UI通知用の不変データ。
    /// BoardStateへの反映はBoardState.ApplyAttack()が担当する。
    /// bool項目は保持せず、nullable/Listの内容から計算プロパティとして導出する
    /// （「発生した」というboolと実体データが矛盾するインスタンスを作れないようにするため）。
    /// </summary>
    public readonly struct AttackOutcome
    {
        public readonly int damage;
        public readonly bool targetDefeated;
        public readonly IReadOnlyList<OnHitStatusEffect> appliedEffects;

        public readonly Vector2Int? knockbackDestination;
        public bool KnockbackOccurred => knockbackDestination.HasValue;

        // 追撃移動の候補マス（選択肢の提示のみ）。
        // 実際の移動はバトル側がBoardState.ApplyMoveを別途呼び出して行う。
        public readonly IReadOnlyList<Vector2Int> pursuitMoveCandidates;
        public bool PursuitMoveAvailable => pursuitMoveCandidates.Count > 0;

        public readonly ChainCollisionResult? chainCollisionResult;
        public bool ChainCollisionOccurred => chainCollisionResult.HasValue;

        public AttackOutcome(
            int damage,
            bool targetDefeated,
            IReadOnlyList<OnHitStatusEffect> appliedEffects,
            Vector2Int? knockbackDestination,
            IReadOnlyList<Vector2Int> pursuitMoveCandidates,
            ChainCollisionResult? chainCollisionResult)
        {
            this.damage = damage;
            this.targetDefeated = targetDefeated;
            this.appliedEffects = appliedEffects ?? Array.Empty<OnHitStatusEffect>();
            this.knockbackDestination = knockbackDestination;
            this.pursuitMoveCandidates = pursuitMoveCandidates ?? Array.Empty<Vector2Int>();
            this.chainCollisionResult = chainCollisionResult;
        }
    }
}
