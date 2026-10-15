using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// Chebyss.Battle（ターン制・新スケール）専用の装備ジェネレーター。
    /// 旧EquipmentGenerator（旧ゲームが使用）は変更せず残し、レアリティ抽選の重み・名前プール・
    /// itemIdの生成方式のみ同じものを踏襲する。ボーナスの種類・数値は新スケール用に独立して定義する。
    /// </summary>
    public static class BattleEquipmentGenerator
    {
        private static readonly string[] CommonNames = { "かけらの指輪", "古びた剣", "粗末な盾", "布のマント" };
        private static readonly string[] RareNames = { "鋼の指輪", "魔法の剣", "鉄壁の盾", "知恵のマント" };
        private static readonly string[] EpicNames = { "竜鱗の指輪", "業火の剣", "不落の盾", "賢者のマント" };
        private static readonly string[] LegendaryNames = { "星辰の指輪", "破滅の剣", "絶対の盾", "神話のマント" };

        // 駒がダメージを受ける仕組み（A-1）が実装されたため有効化。
        // falseにすると最大HP+を生成しなくなる（RollBonusValue・FormatBonusのHpFlat分岐は残してある）。
        private const bool EnableHpFlat = true;

        // qualityTier: 1=雑魚敵, 2=中堅敵, 3=強敵（レアリティが上振れしやすくなる）
        public static EquipmentInstance GenerateRandomEquipment(int qualityTier)
        {
            EquipmentRarity rarity = RollRarity(qualityTier);

            return new EquipmentInstance
            {
                itemId = System.Guid.NewGuid().ToString(),
                rarity = rarity,
                itemName = PickFlavorName(rarity),
                bonuses = GenerateBonuses(rarity)
            };
        }

        // 旧EquipmentGenerator.RollRarityと同じ重み
        private static EquipmentRarity RollRarity(int qualityTier)
        {
            float commonW, rareW, epicW;

            switch (qualityTier)
            {
                case 3:  commonW = 0.20f; rareW = 0.40f; epicW = 0.30f; break;
                case 2:  commonW = 0.45f; rareW = 0.35f; epicW = 0.17f; break;
                default: commonW = 0.70f; rareW = 0.25f; epicW = 0.05f; break;
            }

            float roll = Random.value;

            if (roll < commonW) return EquipmentRarity.Common;
            roll -= commonW;

            if (roll < rareW) return EquipmentRarity.Rare;
            roll -= rareW;

            if (roll < epicW) return EquipmentRarity.Epic;

            return EquipmentRarity.Legendary;
        }

        private static string PickFlavorName(EquipmentRarity rarity)
        {
            string[] pool;
            switch (rarity)
            {
                case EquipmentRarity.Common: pool = CommonNames; break;
                case EquipmentRarity.Rare:   pool = RareNames; break;
                case EquipmentRarity.Epic:   pool = EpicNames; break;
                default:                     pool = LegendaryNames; break;
            }
            return pool[Random.Range(0, pool.Length)];
        }

        private static List<EquipmentStatBonus> GenerateBonuses(EquipmentRarity rarity)
        {
            var pool = BuildCandidatePool(rarity, out int slotCount);

            // シャッフルして先頭から取る＝同じ種類は重複しない
            for (int i = 0; i < pool.Count; i++)
            {
                int r = Random.Range(i, pool.Count);
                (pool[i], pool[r]) = (pool[r], pool[i]);
            }

            int count = Mathf.Min(slotCount, pool.Count);
            var bonuses = new List<EquipmentStatBonus>();
            for (int i = 0; i < count; i++)
                bonuses.Add(RollBonusValue(pool[i], rarity));

            return bonuses;
        }

        // レアリティごとの候補プールとボーナスの個数（生成・+1版の追加分の両方で同じ条件を使う）
        private static List<EquipmentStatType> BuildCandidatePool(EquipmentRarity rarity, out int slotCount)
        {
            var pool = new List<EquipmentStatType> { EquipmentStatType.AttackFlat };

            switch (rarity)
            {
                case EquipmentRarity.Common:
                    slotCount = 1;
                    break;
                case EquipmentRarity.Rare:
                    slotCount = 1;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.DashRangeBonus);
                    break;
                case EquipmentRarity.Epic:
                    slotCount = 2;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.BonusActions);
                    pool.Add(EquipmentStatType.DashRangeBonus);
                    break;
                default: // Legendary
                    slotCount = 3;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.BonusActions);
                    pool.Add(EquipmentStatType.DashRangeBonus);
                    break;
            }

            if (EnableHpFlat) pool.Add(EquipmentStatType.HpFlat);

            return pool;
        }

        private static string[] GetNames(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Common: return CommonNames;
                case EquipmentRarity.Rare:   return RareNames;
                case EquipmentRarity.Epic:   return EpicNames;
                default:                     return LegendaryNames;
            }
        }

        // 盤面で拾ったときの+1版を作る。Legendaryはこれ以上上がらないためnullを返す。
        // 種類（名前の並び順）とボーナスの種類を維持し、値は新しいレアリティで引き直す
        // （元の値より小さくならないようmaxを取る）。ボーナスの個数が増える場合は、候補から不足分を追加する。
        public static EquipmentInstance CreateUpgraded(EquipmentInstance baseItem)
        {
            if (baseItem == null || baseItem.rarity == EquipmentRarity.Legendary) return null;

            var newRarity = baseItem.rarity + 1;

            return new EquipmentInstance
            {
                itemId = System.Guid.NewGuid().ToString(),
                rarity = newRarity,
                itemName = GetUpgradedName(baseItem, newRarity),
                bonuses = GetUpgradedBonuses(baseItem, newRarity)
            };
        }

        // 現在のレアリティの名前の配列での位置を、新しいレアリティの配列でも維持する（例：古びた剣→魔法の剣）
        private static string GetUpgradedName(EquipmentInstance baseItem, EquipmentRarity newRarity)
        {
            int index = System.Array.IndexOf(GetNames(baseItem.rarity), baseItem.itemName);
            var newNames = GetNames(newRarity);

            if (index >= 0 && index < newNames.Length) return newNames[index];
            return PickFlavorName(newRarity);
        }

        private static List<EquipmentStatBonus> GetUpgradedBonuses(EquipmentInstance baseItem, EquipmentRarity newRarity)
        {
            var result = new List<EquipmentStatBonus>();
            var present = new HashSet<EquipmentStatType>();

            if (baseItem.bonuses != null)
            {
                foreach (var original in baseItem.bonuses)
                {
                    var rolled = RollBonusValue(original.statType, newRarity);
                    rolled.value = Mathf.Max(rolled.value, original.value); // 元より下がらない
                    result.Add(rolled);
                    present.Add(original.statType);
                }
            }

            // 新しいレアリティのボーナスの個数が増える場合は、まだ含まれていない種類から不足分を追加する
            var pool = BuildCandidatePool(newRarity, out int slotCount);
            var candidates = new List<EquipmentStatType>();
            foreach (var type in pool)
                if (!present.Contains(type)) candidates.Add(type);

            while (result.Count < slotCount && candidates.Count > 0)
            {
                int pick = Random.Range(0, candidates.Count);
                result.Add(RollBonusValue(candidates[pick], newRarity));
                candidates.RemoveAt(pick);
            }

            return result;
        }

        private static EquipmentStatBonus RollBonusValue(EquipmentStatType type, EquipmentRarity rarity)
        {
            var bonus = new EquipmentStatBonus { statType = type };

            switch (type)
            {
                case EquipmentStatType.AttackFlat:
                    switch (rarity)
                    {
                        case EquipmentRarity.Common: bonus.value = 1; break;
                        case EquipmentRarity.Rare:   bonus.value = Random.Range(1, 3); break; // 1〜2（intのRangeは上限を含まない）
                        case EquipmentRarity.Epic:   bonus.value = Random.Range(2, 4); break; // 2〜3
                        default:                     bonus.value = 3; break;
                    }
                    break;

                case EquipmentStatType.HpFlat:
                    switch (rarity)
                    {
                        case EquipmentRarity.Common: bonus.value = Random.Range(2, 4); break;  // 2〜3
                        case EquipmentRarity.Rare:   bonus.value = Random.Range(3, 6); break;  // 3〜5
                        case EquipmentRarity.Epic:   bonus.value = Random.Range(5, 8); break;  // 5〜7
                        default:                     bonus.value = Random.Range(7, 10); break; // 7〜9
                    }
                    break;

                case EquipmentStatType.DistanceBonusRate:
                    switch (rarity)
                    {
                        case EquipmentRarity.Rare: bonus.value = RoundTo2(Random.Range(0.25f, 0.35f)); break;
                        case EquipmentRarity.Epic: bonus.value = RoundTo2(Random.Range(0.35f, 0.50f)); break;
                        default:                   bonus.value = RoundTo2(Random.Range(0.50f, 0.75f)); break;
                    }
                    break;

                case EquipmentStatType.BonusActions:
                    bonus.value = 1f;
                    break;

                case EquipmentStatType.SenseLevel:
                    bonus.value = 1f; // 固定
                    break;

                case EquipmentStatType.DashRangeBonus:
                    bonus.value = 1f; // 固定
                    break;
            }

            return bonus;
        }

        private static float RoundTo2(float v) => Mathf.Round(v * 100f) / 100f;

        // UI表示用：ボーナス1件を短いテキストに変換
        public static string FormatBonus(EquipmentStatBonus bonus)
        {
            switch (bonus.statType)
            {
                case EquipmentStatType.AttackFlat:
                    return $"ATK+{Mathf.RoundToInt(bonus.value)}";
                case EquipmentStatType.HpFlat:
                    return $"HP+{Mathf.RoundToInt(bonus.value)}";
                case EquipmentStatType.BonusActions:
                    return $"行動回数+{Mathf.RoundToInt(bonus.value)}";
                case EquipmentStatType.SenseLevel:
                    return $"感知+{Mathf.RoundToInt(bonus.value)}";
                case EquipmentStatType.DashRangeBonus:
                    return $"突進距離+{Mathf.RoundToInt(bonus.value)}（ナイト用）";
                case EquipmentStatType.DistanceBonusRate:
                    return $"距離ボーナス+{Mathf.RoundToInt(bonus.value * 100f)}%（ナイト用）";
                default:
                    // 新ジェネレーターでは生成しない種類。旧EquipmentGeneratorと同じ表記にフォールバックする
                    return EquipmentGenerator.FormatBonus(bonus);
            }
        }
    }
}
