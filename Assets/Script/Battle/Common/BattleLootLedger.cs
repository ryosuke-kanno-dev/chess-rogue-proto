using System.Collections.Generic;

namespace Chebyss.Battle
{
    // このバトル中に獲得した装備の一覧。BattleExperienceLedgerと同じ寿命
    // （BattleTurnControllerと一緒に生成され、バトルが終わるまで蓄積される）。
    public class BattleLootLedger
    {
        private readonly List<EquipmentInstance> collected = new();

        public IReadOnlyList<EquipmentInstance> Collected => collected;

        // 拾得ボーナス（レアリティ+1版）として獲得した数
        public int BoostedCount { get; private set; }

        public void Add(EquipmentInstance item)
        {
            if (item == null) return;
            collected.Add(item);
        }

        // 拾得ボーナスの+1版を記録する（Collectedにも追加し、BoostedCountを+1する）
        public void AddBoosted(EquipmentInstance item)
        {
            if (item == null) return;
            collected.Add(item);
            BoostedCount++;
        }
    }
}
