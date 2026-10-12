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

        // ラン結果画面用の統計
        public static int FloorsCleared { get; private set; }
        public static int EnemiesDefeated { get; private set; }
        public static int ItemsCollected { get; private set; }
        public static int PiecesLost { get; private set; }

        // 1階の勝利を記録する（バトルごとに1回だけ呼ぶ）
        public static void RecordVictory(int enemiesDefeated, int itemsCollected, int piecesLost)
        {
            FloorsCleared++;
            EnemiesDefeated += enemiesDefeated;
            ItemsCollected += itemsCollected;
            PiecesLost += piecesLost;
        }

        private static readonly PieceType[] EquippableTypes =
        {
            PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Pawn
        };

        private static readonly Dictionary<PieceType, EquipmentInstance[]> slots = new();

        // 倒れた駒（墓地）。墓地の駒は次のバトルに出られず、装備もできない。
        private static readonly HashSet<PieceType> deadPieces = new();

        // 前回のバトルの駒種ごとの配置マス（次のバトルの自動配置に使う）
        private static readonly Dictionary<PieceType, Vector2Int> lastPlacement = new();

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
            deadPieces.Clear();
            lastPlacement.Clear();
            foreach (var type in EquippableTypes)
                slots[type] = new EquipmentInstance[SlotsPerPiece];

            CurrentFloor = 1;
            NextEncounter = null;
            RunSeed = new System.Random().Next();

            FloorsCleared = 0;
            EnemiesDefeated = 0;
            ItemsCollected = 0;
            PiecesLost = 0;
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

        // 前回の配置を置き換える
        public static void SavePlacement(IEnumerable<KeyValuePair<PieceType, Vector2Int>> placement)
        {
            lastPlacement.Clear();
            if (placement == null) return;

            foreach (var kvp in placement)
                lastPlacement[kvp.Key] = kvp.Value;
        }

        public static bool TryGetLastPlacement(PieceType type, out Vector2Int pos)
        {
            return lastPlacement.TryGetValue(type, out pos);
        }

        public static bool IsPieceDead(PieceType type) => deadPieces.Contains(type);

        public static IReadOnlyCollection<PieceType> DeadPieces =>
            new ReadOnlyCollection<PieceType>(new List<PieceType>(deadPieces));

        // 駒を墓地へ送る。そのスロットの装備は全て所持品へ戻る。
        // 対象外の駒種、または既に墓地にいる場合は何もしない。
        public static void MarkPieceDead(PieceType type)
        {
            if (!slots.TryGetValue(type, out var array)) return;
            if (deadPieces.Contains(type)) return;

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null) continue;
                Inventory.Add(array[i]);
                array[i] = null;
            }

            deadPieces.Add(type);
        }

        // 墓地の駒を、所持品の装備1つを消費して蘇生する。蘇生した駒の装備スロットは空のまま。
        public static bool TryRevive(PieceType type, EquipmentInstance sacrifice)
        {
            if (!deadPieces.Contains(type)) return false;
            if (sacrifice == null || !Inventory.Contains(sacrifice)) return false;

            Inventory.Remove(sacrifice); // 消費（所持品へ戻さない）
            deadPieces.Remove(type);
            return true;
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
            if (deadPieces.Contains(type)) return false;
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
            if (deadPieces.Contains(type)) return false;
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
