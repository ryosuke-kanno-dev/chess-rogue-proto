using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Enemy/Straight Advance Pattern", fileName = "StraightAdvancePattern")]
    public class StraightAdvanceMovementPatternSO : EnemyMovementPatternSO
    {
        public Vector2Int advanceDirection = new(0, -1);
    }
}
