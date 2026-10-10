using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// シーン再読み込み（階の切り替え）をまたいで維持する、ラン全体の状態。
    /// 未装備の所持品・各駒の装備スロット・現在の階・次の階の敵計画を保持する。
    /// 装備スロットの変更は必ずこのクラスのメソッド経由で行う（所持品との整合性を保つため）。
    /// </summary>
    public static class RunState
    {
        public const int SlotsPerPiece = 3;
        public const int MaxSenseLevel = 3;

        public static PlayerInventory Inventory { get; private set; }
        public static int CurrentFloor { get; set; }
        public static EncounterPlan NextEncounter { get; set; }

        // ラン全体で固定の乱数シード（階ごとの敵生成に使う）。Reset()のたびに新しく決まる。
        public static int RunSeed { get; private set; }

        private static readonly PieceType[] EquippableTypes =
        {
            PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Pawn
        };

        private static readonly Dictionary<PieceType, EquipmentInstance[]> slots = new();

        static RunState()
        {
            Reset();
        }

        // Domain Reload無効の設定でも、再生開始のたびに前回のラン状態が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayStart()
        {
            Reset();
        }

        public static void Reset()
        {
            Inventory = new PlayerInventory();
            slots.Clear();
            foreach (var type in EquippableTypes)
                slots[type] = new EquipmentInstance[SlotsPerPiece];

            CurrentFloor = 1;
            NextEncounter = null;
            RunSeed = new System.Random().Next();
        }

        // 全駒種・全スロットの装備のうち、SenseLevelのボーナスの値を合計して整数（四捨五入）で返す。
        // 上限（MaxSenseLevel）の丸めは呼び出し側で行う。
        public static int GetTotalSenseValue()
        {
            float total = 0f;
            foreach (var array in slots.Values)
            {
                foreach (var item in array)
                {
                    if (item == null || item.bonuses == null) continue;

                    foreach (var bonus in item.bonuses)
                    {
                        if (bonus.statType == EquipmentStatType.SenseLevel)
                            total += bonus.value;
                    }
                }
            }
            return Mathf.RoundToInt(total);
        }

        public static IReadOnlyList<EquipmentInstance> GetEquipped(PieceType type)
        {
            if (!slots.TryGetValue(type, out var array))
                return new ReadOnlyCollection<EquipmentInstance>(new EquipmentInstance[0]);

            return new ReadOnlyCollection<EquipmentInstance>(array);
        }

        // 所持品のitemだけを装備できる。スロットが埋まっていた場合は入れ替え（元の装備は所持品へ戻る）。
        public static bool TryEquip(PieceType type, int slotIndex, EquipmentInstance item)
        {
            if (!TryGetSlots(type, slotIndex, out var array)) return false;
            if (item == null || !Inventory.Contains(item)) return false;

            var previous = array[slotIndex];

            Inventory.Remove(item);
            array[slotIndex] = item;

            if (previous != null) Inventory.Add(previous);
            return true;
        }

        // 外した装備は所持品へ戻る。空きスロットならfalse。
        public static bool Unequip(PieceType type, int slotIndex)
        {
            if (!TryGetSlots(type, slotIndex, out var array)) return false;

            var item = array[slotIndex];
            if (item == null) return false;

            array[slotIndex] = null;
            Inventory.Add(item);
            return true;
        }

        private static bool TryGetSlots(PieceType type, int slotIndex, out EquipmentInstance[] array)
        {
            array = null;
            if (!slots.TryGetValue(type, out array)) return false;
            return slotIndex >= 0 && slotIndex < array.Length;
        }
    }
}
