namespace Chebyss.Battle
{
    // 1回の攻撃で発生したダメージの記録。経験値計算（ステップ3）の元データとなる。
    public readonly struct DamageRecord
    {
        public readonly int attackerInstanceId;
        public readonly int targetInstanceId;
        public readonly int damage;
        public readonly bool wasKillingBlow;

        public DamageRecord(int attackerInstanceId, int targetInstanceId, int damage, bool wasKillingBlow)
        {
            this.attackerInstanceId = attackerInstanceId;
            this.targetInstanceId = targetInstanceId;
            this.damage = damage;
            this.wasKillingBlow = wasKillingBlow;
        }
    }
}
