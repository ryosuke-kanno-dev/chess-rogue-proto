using UnityEngine;

namespace Chebyss.Battle
{
    public readonly struct EnemyActionRecord
    {
        public readonly int instanceId;
        public readonly Vector2Int startPosition;
        public readonly bool wasStunned;
        public readonly Vector2Int? moveDestination;
        public readonly bool attackedKing;

        public EnemyActionRecord(int instanceId, Vector2Int startPosition, bool wasStunned, Vector2Int? moveDestination, bool attackedKing)
        {
            this.instanceId = instanceId;
            this.startPosition = startPosition;
            this.wasStunned = wasStunned;
            this.moveDestination = moveDestination;
            this.attackedKing = attackedKing;
        }
    }
}
