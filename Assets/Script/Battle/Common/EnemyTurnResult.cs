namespace Chebyss.Battle
{
    public readonly struct EnemyTurnResult
    {
        public readonly bool kingDefeated;
        public EnemyTurnResult(bool kingDefeated) => this.kingDefeated = kingDefeated;
    }
}
