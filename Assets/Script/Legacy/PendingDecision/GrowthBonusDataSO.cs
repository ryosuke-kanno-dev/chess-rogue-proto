using System.Collections.Generic;
using UnityEngine;

// ステップ24: 合成（★2進化）時の3択成長ボーナスのデータ化。
// DebugGameManagerのGenerateGrowthOptions / ApplyGrowthChoice / UI_GetGrowthOptionLabelがここを参照する。
// アセット未設定/該当エントリ無しの場合は既存のハードコード値へ自動フォールバックする。
[CreateAssetMenu(fileName = "GrowthBonusData", menuName = "Game/Growth Bonus Data")]
public class GrowthBonusDataSO : ScriptableObject
{
  [System.Serializable]
  public class GrowthBonusEntry
  {
    public GrowthType type;
    public string title;
    public string description;
    [Tooltip("意味はtypeに依存: AttackUp=攻撃力加算値 / HpUp=最大HP加算値 / SpeedUp=攻撃間隔の短縮率(0.2=20%短縮) / Lifesteal=吸血率の加算値(0.2=20%)")]
    public float value;
  }

  // 既存のハードコード値（ステップ29でATK/HPを×10したスケール）をそのまま初期値として持たせてある
  public List<GrowthBonusEntry> options = new List<GrowthBonusEntry>
  {
    new GrowthBonusEntry { type = GrowthType.AttackUp,  title = "攻撃強化", description = "攻撃力 +150",       value = 150f },
    new GrowthBonusEntry { type = GrowthType.HpUp,      title = "耐久強化", description = "最大HP +1000",      value = 1000f },
    new GrowthBonusEntry { type = GrowthType.SpeedUp,   title = "敏捷強化", description = "攻撃速度 +20%",      value = 0.2f },
    new GrowthBonusEntry { type = GrowthType.Lifesteal, title = "吸血付与", description = "攻撃時 20% 吸血",   value = 0.2f },
  };

  public GrowthBonusEntry GetEntry(GrowthType type)
  {
    return options.Find(o => o.type == type);
  }
}
