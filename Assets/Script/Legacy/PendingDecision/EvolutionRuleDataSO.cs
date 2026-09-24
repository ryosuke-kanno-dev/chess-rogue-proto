using System.Collections.Generic;
using UnityEngine;

// 課題【★2→★3合成の育成履歴分岐システム】: ★2進化時に選んだGrowthType（攻撃/HP/速度/吸血）の傾向によって、
// ★2→★3進化時にどの派生駒（フレーバー名+追加ステータス）になるかを表す。
public enum EvolutionVariant
{
  AttackFocused,    // 主属性: 攻撃（2体以上が攻撃を選択）
  HpFocused,        // 主属性: HP
  SpeedFocused,     // 主属性: 速度
  LifestealFocused, // 主属性: 吸血
  Balanced,         // 主属性なし（3体とも別のGrowthTypeを選択）
}

[CreateAssetMenu(fileName = "EvolutionRuleData", menuName = "Game/Evolution Rule Data")]
public class EvolutionRuleDataSO : ScriptableObject
{
  [System.Serializable]
  public class EvolutionRuleEntry
  {
    public PieceType type;
    public EvolutionVariant variant;
    [Tooltip("進化後のフレーバー名（例: 「吸血馬」「破城の戦車」）")]
    public string evolvedName;
    [Tooltip("ツールチップに追記する説明文")]
    [TextArea(2, 3)]
    public string evolvedDescription;
    [Tooltip("RankUp()による通常成長に加えて上乗せする追加倍率（例: 0.15なら+15%）")]
    public float attackBonusMultiplier;
    public float hpBonusMultiplier;
    [Tooltip("attackIntervalをこの割合だけ追加短縮する（例: 0.1なら通常成長後にさらに10%短縮）")]
    public float speedBonusRate;
    [Tooltip("lifestealRateへ加算する追加値")]
    public float lifestealBonusRate;
    [Tooltip("trueの場合、装備スロットを1つ追加する（Balanced用の特別ボーナス想定だが、任意のエントリで有効化してよい）")]
    public bool grantsExtraEquipSlot;
  }

  // 対象4駒種（ナイト/ルーク/ビショップ/クイーン）×5パターン(AttackFocused/HpFocused/SpeedFocused/
  // LifestealFocused/Balanced) = 20エントリを既定値として持たせてある。
  // 数値は小さめの上乗せ（該当する主属性の倍率を0.15程度、他は0）を初期値とし、
  // Balanced用の5エントリはgrantsExtraEquipSlot=trueで4項目とも0.05程度の小さい値にしてある
  // （プレイテスト後の調整前提のため、厳密なバランスは今回は問わない）。
  public List<EvolutionRuleEntry> rules = new List<EvolutionRuleEntry>
  {
    // ───────── ナイト（蹩馬腿・機動系） ─────────
    new EvolutionRuleEntry {
      type = PieceType.Knight, variant = EvolutionVariant.AttackFocused,
      evolvedName = "疾風の騎兵", evolvedDescription = "駆け抜けざまに強打を叩き込む、攻勢特化のナイト。",
      attackBonusMultiplier = 0.15f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Knight, variant = EvolutionVariant.HpFocused,
      evolvedName = "鉄蹄の重騎兵", evolvedDescription = "重装の馬鎧をまとい、前線に居座り続ける頑丈なナイト。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0.15f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Knight, variant = EvolutionVariant.SpeedFocused,
      evolvedName = "旋風の遊撃兵", evolvedDescription = "目にも留まらぬ速さで戦場を駆け回る遊撃型のナイト。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0.15f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Knight, variant = EvolutionVariant.LifestealFocused,
      evolvedName = "吸血馬", evolvedDescription = "斬りつけるたびに敵の生命力を喰らい、自らを癒す呪われたナイト。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0.15f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Knight, variant = EvolutionVariant.Balanced,
      evolvedName = "百戦の遊撃隊", evolvedDescription = "偏りなく鍛え上げられた、あらゆる戦況に対応できる歴戦のナイト。",
      attackBonusMultiplier = 0.05f, hpBonusMultiplier = 0.05f, speedBonusRate = 0.05f, lifestealBonusRate = 0.05f, grantsExtraEquipSlot = true
    },

    // ───────── ルーク（戦車・突破力系） ─────────
    new EvolutionRuleEntry {
      type = PieceType.Rook, variant = EvolutionVariant.AttackFocused,
      evolvedName = "破城の戦車", evolvedDescription = "城壁すら打ち砕く一撃を放つ、攻城特化のルーク。",
      attackBonusMultiplier = 0.15f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Rook, variant = EvolutionVariant.HpFocused,
      evolvedName = "不落の要塞戦車", evolvedDescription = "分厚い装甲で前線を支え続ける、要塞さながらのルーク。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0.15f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Rook, variant = EvolutionVariant.SpeedFocused,
      evolvedName = "疾走戦車", evolvedDescription = "重量を感じさせぬ速度で直線を制圧する、快速型のルーク。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0.15f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Rook, variant = EvolutionVariant.LifestealFocused,
      evolvedName = "喰らう鉄輪", evolvedDescription = "踏み潰した敵の力を糧に、自らの装甲を再生させる鉄輪のルーク。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0.15f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Rook, variant = EvolutionVariant.Balanced,
      evolvedName = "万能戦車", evolvedDescription = "攻守速のいずれにも秀でた、隙のない万能型のルーク。",
      attackBonusMultiplier = 0.05f, hpBonusMultiplier = 0.05f, speedBonusRate = 0.05f, lifestealBonusRate = 0.05f, grantsExtraEquipSlot = true
    },

    // ───────── ビショップ（象・守備的支援系） ─────────
    new EvolutionRuleEntry {
      type = PieceType.Bishop, variant = EvolutionVariant.AttackFocused,
      evolvedName = "破戒の法象", evolvedDescription = "慈悲を捨て、聖なる怒りの弾を撃ち放つ攻性のビショップ。",
      attackBonusMultiplier = 0.15f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Bishop, variant = EvolutionVariant.HpFocused,
      evolvedName = "不動の聖壁", evolvedDescription = "如何なる猛攻にも揺るがない、守護に徹したビショップ。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0.15f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Bishop, variant = EvolutionVariant.SpeedFocused,
      evolvedName = "疾駆の巡礼象", evolvedDescription = "軽やかな足取りで戦線を渡り歩き、次々と加護を授けるビショップ。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0.15f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Bishop, variant = EvolutionVariant.LifestealFocused,
      evolvedName = "生命の泉の象使い", evolvedDescription = "敵の生命力を吸い上げ、癒しの奇跡へと変える象使い。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0.15f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Bishop, variant = EvolutionVariant.Balanced,
      evolvedName = "賢者の白象", evolvedDescription = "攻撃・回復・防御のすべてに通じた、賢者の域に達したビショップ。",
      attackBonusMultiplier = 0.05f, hpBonusMultiplier = 0.05f, speedBonusRate = 0.05f, lifestealBonusRate = 0.05f, grantsExtraEquipSlot = true
    },

    // ───────── クイーン（参謀・王の側で輝く系） ─────────
    new EvolutionRuleEntry {
      type = PieceType.Queen, variant = EvolutionVariant.AttackFocused,
      evolvedName = "紅蓮の軍師", evolvedDescription = "戦場を焼き尽くす魔弾を操る、攻勢に徹した軍師。",
      attackBonusMultiplier = 0.15f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Queen, variant = EvolutionVariant.HpFocused,
      evolvedName = "王の盾となる参謀", evolvedDescription = "自らの身を盾に、あらゆる猛攻から陣を守り抜く参謀。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0.15f, speedBonusRate = 0f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Queen, variant = EvolutionVariant.SpeedFocused,
      evolvedName = "疾風の参謀", evolvedDescription = "戦況の変化を読み切り、誰よりも早く魔弾を撃ち込む参謀。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0.15f, lifestealBonusRate = 0f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Queen, variant = EvolutionVariant.LifestealFocused,
      evolvedName = "魂喰らいの魔女", evolvedDescription = "撃ち抜いた敵の魂を糧とし、自らの力へと変換する禁断の参謀。",
      attackBonusMultiplier = 0f, hpBonusMultiplier = 0f, speedBonusRate = 0f, lifestealBonusRate = 0.15f, grantsExtraEquipSlot = false
    },
    new EvolutionRuleEntry {
      type = PieceType.Queen, variant = EvolutionVariant.Balanced,
      evolvedName = "全知の大参謀", evolvedDescription = "攻撃・耐久・速度・生命力、すべての才を兼ね備えた王国随一の大参謀。",
      attackBonusMultiplier = 0.05f, hpBonusMultiplier = 0.05f, speedBonusRate = 0.05f, lifestealBonusRate = 0.05f, grantsExtraEquipSlot = true
    },
  };

  public EvolutionRuleEntry GetRule(PieceType type, EvolutionVariant variant)
  {
    return rules.Find(r => r.type == type && r.variant == variant);
  }
}
