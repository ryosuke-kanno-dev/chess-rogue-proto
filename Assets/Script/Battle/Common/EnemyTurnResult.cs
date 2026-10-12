using System;
using System.Collections.Generic;

namespace Chebyss.Battle
{
    public readonly struct EnemyTurnResult
    {
        public readonly bool kingDefeated;
        public readonly IReadOnlyList<EnemyActionRecord> actions;

        // キング以外の味方駒がいたのに、このターンで全て倒れた
        public readonly bool allPiecesDefeated;

        public EnemyTurnResult(bool kingDefeated, IReadOnlyList<EnemyActionRecord> actions, bool allPiecesDefeated = false)
        {
            this.kingDefeated = kingDefeated;
            this.actions = actions ?? Array.Empty<EnemyActionRecord>();
            this.allPiecesDefeated = allPiecesDefeated;
        }
    }
}
