using System.Collections.Generic;

namespace Chebyss.Battle
{
    // このバトル中に各駒（instanceId）が蓄積した経験値。BattleDamageLogと同じ寿命。
    public class BattleExperienceLedger
    {
        private readonly Dictionary<int, int> xpByInstanceId = new();

        public void AddXp(int instanceId, int amount)
        {
            xpByInstanceId.TryGetValue(instanceId, out var current);
            xpByInstanceId[instanceId] = current + amount;
        }

        public int GetXp(int instanceId) =>
            xpByInstanceId.TryGetValue(instanceId, out var v) ? v : 0;
    }
}
