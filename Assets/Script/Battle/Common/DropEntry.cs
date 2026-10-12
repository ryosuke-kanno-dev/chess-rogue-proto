using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// 盤面に落ちているドロップ1件。BoardStateが保持する。
    /// EquipmentInstanceは既存のアイテムデータ（グローバル名前空間）をそのまま使う。
    /// </summary>
    public readonly struct DropEntry
    {
        public readonly int dropId;
        public readonly EquipmentInstance item;

        // 駒が停止して拾得したときに獲得する+1版（レアリティが1つ上）。nullなら+1版なし。
        public readonly EquipmentInstance boostedItem;
        public readonly Vector2Int position;

        public DropEntry(int dropId, EquipmentInstance item, EquipmentInstance boostedItem, Vector2Int position)
        {
            this.dropId = dropId;
            this.item = item;
            this.boostedItem = boostedItem;
            this.position = position;
        }
    }
}
