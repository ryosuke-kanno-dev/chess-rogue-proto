namespace Chebyss.Battle
{
    /// <summary>
    /// 敵の呼び名。ボスは階によって「小ボス／中ボス／ラスボス」と呼び分ける。
    /// ボス階の判定もここに集約する（EncounterGeneratorと表示層で同じ判定を使うため）。
    /// </summary>
    public static class EnemyNames
    {
        // 間隔ごとの階、または最終階がボス階
        public static bool IsBossFloor(int floor, int bossFloorInterval, int finalFloor)
        {
            return (bossFloorInterval > 0 && floor % bossFloorInterval == 0) || floor == finalFloor;
        }

        // ボス階でなければ""。最終階はラスボス、間隔の1回目は小ボス、2回目は中ボス、それ以外はボス。
        public static string GetBossRankName(int floor, int bossFloorInterval, int finalFloor)
        {
            if (!IsBossFloor(floor, bossFloorInterval, finalFloor)) return "";
            if (floor == finalFloor) return "ラスボス";

            int number = bossFloorInterval > 0 ? floor / bossFloorInterval : 0;
            switch (number)
            {
                case 1:  return "小ボス";
                case 2:  return "中ボス";
                default: return "ボス";
            }
        }

        public static string GetDisplayName(EnemyArchetype archetype, int floor, int bossFloorInterval, int finalFloor)
        {
            switch (archetype)
            {
                case EnemyArchetype.Boss:
                    var rank = GetBossRankName(floor, bossFloorInterval, finalFloor);
                    return string.IsNullOrEmpty(rank) ? "ボス" : rank;
                case EnemyArchetype.Scout:     return "スカウト";
                case EnemyArchetype.Retreater: return "逃げる敵";
                default:                       return archetype.ToString();
            }
        }
    }
}
