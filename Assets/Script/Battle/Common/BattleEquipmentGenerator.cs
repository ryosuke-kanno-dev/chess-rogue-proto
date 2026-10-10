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

        // 駒がダメージを受ける仕組み（仕様書 Phase 2 項目6：駒のHP・墓地システム）が
        // 実装されるまで、最大HP+は盤面で効果がないため生成しない。
        // 実装後にtrueへ変えると、全レアリティの候補プールにHpFlatが加わる
        // （RollBonusValue・FormatBonusのHpFlat分岐は復活用に残してある）。
        private const bool EnableHpFlat = false;

        // テスト用：trueにするとCommonの候補プールにもSenseLevel（感知）が加わる。
        // 通常はfalse（感知はRare以上のみ）。
        private const bool AllowSenseOnCommon = false;

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
            int slotCount;
            var pool = new List<EquipmentStatType> { EquipmentStatType.AttackFlat };

            switch (rarity)
            {
                case EquipmentRarity.Common:
                    slotCount = 1;
                    break;
                case EquipmentRarity.Rare:
                    slotCount = 1;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.SenseLevel);
                    break;
                case EquipmentRarity.Epic:
                    slotCount = 2;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.BonusActions);
                    pool.Add(EquipmentStatType.SenseLevel);
                    break;
                default: // Legendary
                    slotCount = 3;
                    pool.Add(EquipmentStatType.DistanceBonusRate);
                    pool.Add(EquipmentStatType.BonusActions);
                    pool.Add(EquipmentStatType.SenseLevel);
                    break;
            }

#pragma warning disable CS0162 // EnableHpFlat=false の間は到達しないコード（意図的）
            if (EnableHpFlat) pool.Add(EquipmentStatType.HpFlat);
            if (AllowSenseOnCommon && rarity == EquipmentRarity.Common) pool.Add(EquipmentStatType.SenseLevel);
#pragma warning restore CS0162

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
                case EquipmentStatType.DistanceBonusRate:
                    return $"距離ボーナス+{Mathf.RoundToInt(bonus.value * 100f)}%（ナイト用）";
                default:
                    // 新ジェネレーターでは生成しない種類。旧EquipmentGeneratorと同じ表記にフォールバックする
                    return EquipmentGenerator.FormatBonus(bonus);
            }
        }
    }
}
