// Unityエディタから調整できるよう[System.Serializable]にしている
// （StatusIndicatorSettings・GameConfig系と同じ方針）。数値は全て仮置き。
[System.Serializable]
public class PawnGrowthRules
{
    public float damageShareXpRate = 0.3f;
    public float killingBlowXpRate = 0.7f;

    // 仮値。固定1戦（ボス1体・スカウト2体）で実際に進化が確認できる程度の値にしている。
    // Phase2・3の数値スケール統一のタイミングで本調整する。
    public int xpToEvolveStar2 = 15;

    // 旧PieceData.EvolveToStar2()と同じ倍率を踏襲（検証済みの値のため）
    public float star2MaxHpMultiplier = 1.6f;
    public float star2AttackMultiplier = 1.5f;
}
