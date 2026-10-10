namespace Chebyss.Battle
{
    /// <summary>
    /// 敵種ごとのドロップ品質Tierの定義。暫定値のため、調整はこの1箇所だけで行う。
    /// 戻り値はEquipmentGenerator.GenerateRandomEquipment(qualityTier)へそのまま渡す
    /// （1=雑魚, 2=中堅, 3=強敵。高いほどレアリティが上振れしやすい）。
    /// </summary>
    public static class DropRules
    {
        public static int GetQualityTier(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Boss:      return 3;
                case EnemyArchetype.Scout:     return 1;
                case EnemyArchetype.Retreater: return 3;
                default:                       return 1;
            }
        }
    }
}
