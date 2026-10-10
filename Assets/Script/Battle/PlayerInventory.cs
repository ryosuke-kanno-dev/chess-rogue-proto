using System.Collections.Generic;

namespace Chebyss.Battle
{
    /// <summary>
    /// プレイヤーの所持品（未装備のもの）。MonoBehaviourに依存しない。
    /// 容量制限は設けない（ドロップ保証のため、満杯による自動売却等も行わない）。
    /// </summary>
    public class PlayerInventory
    {
        private readonly List<EquipmentInstance> items = new();

        public IReadOnlyList<EquipmentInstance> Items => items;

        public void Add(EquipmentInstance item)
        {
            if (item == null) return;
            items.Add(item);
        }

        // 参照が一致する1件を取り除く。無ければfalse。
        public bool Remove(EquipmentInstance item)
        {
            if (item == null) return false;
            return items.Remove(item);
        }

        public bool Contains(EquipmentInstance item)
        {
            return item != null && items.Contains(item);
        }
    }
}
