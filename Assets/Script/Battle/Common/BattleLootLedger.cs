using System.Collections.Generic;

namespace Chebyss.Battle
{
    // このバトル中に獲得した装備の一覧。BattleExperienceLedgerと同じ寿命
    // （BattleTurnControllerと一緒に生成され、バトルが終わるまで蓄積される）。
    public class BattleLootLedger
    {
        private readonly List<EquipmentInstance> collected = new();

        public IReadOnlyList<EquipmentInstance> Collected => collected;

        public void Add(EquipmentInstance item)
        {
            if (item == null) return;
            collected.Add(item);
        }
    }
}
