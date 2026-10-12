using UnityEngine;

namespace Chebyss.Battle
{
    public readonly struct EnemyActionRecord
    {
        public readonly int instanceId;
        public readonly Vector2Int startPosition;
        public readonly bool wasStunned;
        public readonly Vector2Int? moveDestination;
        public readonly bool attackedKing; // 攻撃対象がキングのときtrue

        // 攻撃対象のマス（キングを含む）。攻撃しなかった場合はnull。
        public readonly Vector2Int? targetPosition;
        // その攻撃のダメージ。攻撃なしは0。
        public readonly int damage;
        // その攻撃で対象が倒れたか。
        public readonly bool targetDefeated;

        // 既存の呼び出し用（攻撃情報は未設定＝攻撃なし）
        public EnemyActionRecord(int instanceId, Vector2Int startPosition, bool wasStunned, Vector2Int? moveDestination, bool attackedKing)
            : this(instanceId, startPosition, wasStunned, moveDestination, attackedKing, null, 0, false)
        {
        }

        public EnemyActionRecord(
            int instanceId,
            Vector2Int startPosition,
            bool wasStunned,
            Vector2Int? moveDestination,
            bool attackedKing,
            Vector2Int? targetPosition,
            int damage,
            bool targetDefeated)
        {
            this.instanceId = instanceId;
            this.startPosition = startPosition;
            this.wasStunned = wasStunned;
            this.moveDestination = moveDestination;
            this.attackedKing = attackedKing;
            this.targetPosition = targetPosition;
            this.damage = damage;
            this.targetDefeated = targetDefeated;
        }
    }
}
