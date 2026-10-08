using System.Collections.Generic;

namespace Chebyss.Battle
{
    // 1回のバトル中に発生した全ての攻撃を記録する。BattleTurnControllerと同じ寿命を持つ
    // （バトル開始時に生成され、バトルが終わるまで蓄積される）。
    public class BattleDamageLog
    {
        private readonly List<DamageRecord> records = new();
        public IReadOnlyList<DamageRecord> Records => records;

        public void Record(int attackerInstanceId, int targetInstanceId, int damage, bool wasKillingBlow)
        {
            records.Add(new DamageRecord(attackerInstanceId, targetInstanceId, damage, wasKillingBlow));
        }

        public int GetTotalDamageDealt(int attackerInstanceId)
        {
            int total = 0;
            foreach (var r in records)
                if (r.attackerInstanceId == attackerInstanceId) total += r.damage;
            return total;
        }

        public bool DidLandKillingBlow(int attackerInstanceId, int targetInstanceId)
        {
            foreach (var r in records)
                if (r.attackerInstanceId == attackerInstanceId && r.targetInstanceId == targetInstanceId && r.wasKillingBlow)
                    return true;
            return false;
        }
    }
}
