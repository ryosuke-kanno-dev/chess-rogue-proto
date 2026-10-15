using System.Collections.Generic;

namespace Chebyss.Battle
{
    public enum SkillBranch
    {
        Info,       // 情報
        Combat,     // 戦闘
        Survival,   // 生存
        Gathering   // 収集
    }

    public sealed class KingSkillDefinition
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string description;
        public readonly int cost;
        public readonly string[] prerequisites;
        public readonly SkillBranch branch;
        public readonly int tier;

        public KingSkillDefinition(string id, string displayName, string description, int cost,
            string[] prerequisites, SkillBranch branch, int tier)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.cost = cost;
            this.prerequisites = prerequisites ?? new string[0];
            this.branch = branch;
            this.tier = tier;
        }
    }

    /// <summary>キングのスキルの一覧。スキルの数値はここに集約する（ScriptableObject化はしない）。</summary>
    public static class KingSkillCatalog
    {
        private static readonly string[] NoPrerequisites = new string[0];

        private static readonly List<KingSkillDefinition> all = new List<KingSkillDefinition>
        {
            new KingSkillDefinition("sense_1", "感知Lv1", "次の階の敵の種類が分かる", 1, NoPrerequisites, SkillBranch.Info, 1),
            new KingSkillDefinition("sense_2", "感知Lv2", "次の階の敵の配置が分かる", 2, new[] { "sense_1" }, SkillBranch.Info, 2),
            new KingSkillDefinition("sense_3", "感知Lv3", "次の階の敵の詳細（HP・ダメージ・行動・ドロップ品質）が分かる", 3, new[] { "sense_2" }, SkillBranch.Info, 3),
            new KingSkillDefinition("appraise_1", "鑑定Lv1", "ドロップを拾うと何のレアリティになるかが分かる", 1, NoPrerequisites, SkillBranch.Info, 1),
            new KingSkillDefinition("appraise_2", "鑑定Lv2", "ドロップを拾った後の装備の内容が分かる", 2, new[] { "appraise_1" }, SkillBranch.Info, 2),
            new KingSkillDefinition("atk_1", "攻撃強化 I", "全ての駒の攻撃力+1", 2, NoPrerequisites, SkillBranch.Combat, 1),
            new KingSkillDefinition("atk_2", "攻撃強化 II", "全ての駒の攻撃力+1", 3, new[] { "atk_1" }, SkillBranch.Combat, 2),
            new KingSkillDefinition("hp_1", "体力強化 I", "全ての駒の最大HP+3", 2, NoPrerequisites, SkillBranch.Survival, 1),
            new KingSkillDefinition("hp_2", "体力強化 II", "全ての駒の最大HP+3", 3, new[] { "hp_1" }, SkillBranch.Survival, 2),
            new KingSkillDefinition("kinghp_1", "王の体力 I", "キングの最大HP+5", 1, NoPrerequisites, SkillBranch.Survival, 1),
            new KingSkillDefinition("kinghp_2", "王の体力 II", "キングの最大HP+5", 2, new[] { "kinghp_1" }, SkillBranch.Survival, 2),
            new KingSkillDefinition("bonus_1", "拾得ボーナス強化", "駒で拾ったとき、25%の確率でレアリティが+1ではなく+2になる", 3, NoPrerequisites, SkillBranch.Gathering, 1),
            new KingSkillDefinition("quality_1", "良品の予感", "全ての敵のドロップ品質+1（最大3）", 4, NoPrerequisites, SkillBranch.Gathering, 1),
        };

        private static readonly Dictionary<string, KingSkillDefinition> byId = BuildIndex();

        public static IReadOnlyList<KingSkillDefinition> All => all;

        public static bool TryGet(string id, out KingSkillDefinition skill)
        {
            if (id == null)
            {
                skill = null;
                return false;
            }
            return byId.TryGetValue(id, out skill);
        }

        private static Dictionary<string, KingSkillDefinition> BuildIndex()
        {
            var index = new Dictionary<string, KingSkillDefinition>();
            foreach (var skill in all) index[skill.id] = skill;
            return index;
        }
    }
}
