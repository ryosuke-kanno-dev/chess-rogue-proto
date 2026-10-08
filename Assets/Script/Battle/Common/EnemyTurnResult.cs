using System;
using System.Collections.Generic;

namespace Chebyss.Battle
{
    public readonly struct EnemyTurnResult
    {
        public readonly bool kingDefeated;
        public readonly IReadOnlyList<EnemyActionRecord> actions;

        public EnemyTurnResult(bool kingDefeated, IReadOnlyList<EnemyActionRecord> actions)
        {
            this.kingDefeated = kingDefeated;
            this.actions = actions ?? Array.Empty<EnemyActionRecord>();
        }
    }
}
