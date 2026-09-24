using System.Collections.Generic;
using UnityEngine;

// ステップ24 → 課題1拡張: 駒（PieceType）ごとの基礎ステータス「および」
// これまでDebugGameManager内にハードコードされていたショップ購入コスト・装備ドロップ率・
// 品質Tier・攻撃範囲もあわせて1つのエントリで一元管理する（＝要件の PieceDataSO に相当）。
//
// PieceData.SetupInitialStats() は maxHp / attack / attackInterval を、
// DebugGameManager の GetCost / GetDropChance / GetQualityTier / GetAttackRange は
// それぞれ対応するフィールドをここから取得する。
// アセット未設定/該当エントリ無しの場合は、既存のハードコード値へ自動フォールバックする。
[CreateAssetMenu(fileName = "UnitStatusData", menuName = "Game/Unit Status Data (Piece Data)")]
public class UnitStatusDataSO : ScriptableObject
{
  [System.Serializable]
  public class UnitStatusEntry
  {
    [Header("基礎ステータス")]
    public PieceType type;
    public string pieceName;
    public int maxHp;
    public int attack;
    public float attackInterval;

    [Header("経済 / ショップ")]
    [Tooltip("ショップでの購入コスト（King/Paladinはショップ購入対象外のため未使用）")]
    public int shopCost;

    [Header("ハクスラ装備ドロップ")]
    [Tooltip("この駒種を撃破した際の装備ドロップ基礎確率（0〜1）")]
    [Range(0f, 1f)]
    public float dropChance;
    [Tooltip("ドロップする装備の品質Tier（EquipmentGenerator.GenerateRandomEquipmentへ渡す値）")]
    public int qualityTier = 1;

    [Header("戦闘")]
    [Tooltip("範囲インジケーター表示・UI表示用の攻撃範囲（実際の攻撃判定はPieceAI側の値を使用）")]
    public float attackRange = 1.8f;

    [Header("駒種別 個別パラメータ（PieceAI.cs 戦闘ロジック）")]
    [Tooltip("【クイーン専用】範囲攻撃の着弾点からの巻き込み半径。\n" +
             "上のattackRangeが「この距離まで届けば発動できる射程」なのに対し、こちらは「着弾後、着弾点から半径何マス以内の敵を巻き込むか」という別概念のため、attackRangeとは分離したフィールドとして持たせている。")]
    public float queenSplashRadius = 1.8f;

    [Tooltip("【ルーク専用】ターゲットが自分と「直線上（同じ行 or 同じ列）」にいるとみなす、X/Z座標差の許容誤差")]
    public float rookStraightLineTolerance = 0.3f;
    [Tooltip("【ルーク専用】直線上の敵へ直線突進した際のダメージ倍率")]
    public float rookChargeMultiplier = 1.5f;

    [Tooltip("【ビショップ専用】射程内の攻撃時に、最もHP割合が低い味方へ与える回復量（固定値）。\n" +
             "ステップ29でATK/HPが概ね×10スケールに調整された一方、この値だけ据え置きになっている可能性があるため、SO化にあわせて要調整。")]
    public int bishopHealAmount = 20;

    [Tooltip("【ポーン専用】ターゲットが自分から見て「斜め」にいるとみなす、X/Z座標差の許容誤差（この値より大きければ斜めと判定）")]
    public float pawnDiagonalDetectionTolerance = 0.5f;
    [Tooltip("【ポーン専用】斜めから攻撃した際のダメージ倍率")]
    public float pawnDiagonalAttackMultiplier = 1.5f;

    [Tooltip("【ナイト専用】L字ジャンプで敵の背後へ回り込む際、ターゲット位置から自分側へ寄せる距離")]
    public float knightJumpDistance = 1.0f;
    [Tooltip("【ナイト専用】L字ジャンプで背後を取った際のクリティカルダメージ倍率")]
    public float knightCriticalMultiplier = 2f;

    [Header("駒種別 個別パラメータ（元ネタ由来の特性）")]
    [Tooltip("【ビショップ専用】この距離以内に敵がいる間、攻撃・回復の性能がbishopMeleeThreatPenaltyRate倍に低下する（前線に晒されると本来の力を発揮できない）")]
    public float bishopMeleeThreatRange = 3.0f;
    [Tooltip("【ビショップ専用】近接ペナルティ発動中の性能倍率（0〜1。0.5なら攻撃・回復とも半減）")]
    [Range(0f, 1f)]
    public float bishopMeleeThreatPenaltyRate = 0.5f;

    [Tooltip("【クイーン専用】自チームのKingからこの距離以内にいる間、攻撃ダメージがqueenKingProximityBonus倍になる（王を守る参謀としての性質）")]
    public float queenKingProximityRadius = 3.0f;
    [Tooltip("【クイーン専用】王への近接ボーナス発動中の攻撃ダメージ倍率（範囲攻撃の巻き込み半径queenSplashRadiusには影響しない）")]
    public float queenKingProximityBonus = 1.3f;

    [Tooltip("【キング専用】Kingが生存している間、自チーム全員の攻撃力に常時付与される上乗せボーナス（例: 0.1で+10%）")]
    public float kingAuraAttackBonus = 0.1f;
    [Tooltip("【キング専用】Kingが生存している間、自チーム全員が受けるダメージに常時適用される軽減率（例: 0.1で-10%）")]
    [Range(0f, 1f)]
    public float kingAuraDamageReduction = 0.1f;

    [Header("課題1: 復活(リバース)システム")]
    [Tooltip("撃破された際に「その場で復活」する確率（0〜1）。Kingは常に別枠でウェーブ毎に全回復するため、\nここでの設定値はKing以外の駒種にのみ適用される（Kingの行の値は無視してよい）")]
    [Range(0f, 1f)]
    public float rebirthRate = 0f;
    [Tooltip("復活時に回復するHPの割合（最大HPに対する比率、0〜1）")]
    [Range(0f, 1f)]
    public float rebirthHpRatio = 0.3f;
    [Tooltip("復活直後、この秒数だけ無敵（被ダメージ無効）になる")]
    public float rebirthInvincibleSeconds = 1.0f;

    [Header("特性表示（ホバーツールチップ用）")]
    [Tooltip("ツールチップに表示する特性名（例: 「直線突進」）")]
    public string abilityName;
    [Tooltip("ツールチップに表示する特性説明文（例: 「射程外でも敵と直線上にいれば、接近しつつ1.5倍ダメージで強襲する」）")]
    [TextArea(2, 4)]
    public string abilityDescription;
  }

  // 既存のハードコード値をそのまま初期値として持たせてある
  public List<UnitStatusEntry> units = new List<UnitStatusEntry>
  {
    new UnitStatusEntry {
      type = PieceType.Pawn,    pieceName = "ポーン",     maxHp = 1200, attack = 200, attackInterval = 2.0f, shopCost = 100, dropChance = 0.15f, qualityTier = 1, attackRange = 1.8f,
      abilityName = "渡河強襲",
      abilityDescription = "斜めから攻撃すると1.5倍のダメージ。盤面中央より奥まで前進（渡河）すると、横方向の敵に対しても同じボーナスが乗るようになる。"
    },
    new UnitStatusEntry {
      type = PieceType.Knight,  pieceName = "ナイト",     maxHp = 1500, attack = 250, attackInterval = 2.0f, shopCost = 200, dropChance = 0.25f, qualityTier = 1, attackRange = 1.8f,
      abilityName = "蹩馬腿（べつばたい）",
      abilityDescription = "射程内は通常攻撃。射程外なら敵の背後へL字ジャンプし2倍のクリティカルダメージ。ただし跳躍方向の隣接マスに駒（味方・敵問わず）がいると、足を封じられてジャンプできない。"
    },
    new UnitStatusEntry {
      type = PieceType.Rook,    pieceName = "ルーク",     maxHp = 2000, attack = 300, attackInterval = 2.5f, shopCost = 300, dropChance = 0.40f, qualityTier = 2, attackRange = 1.8f,
      abilityName = "直線制圧",
      abilityDescription = "自分と同じ行・列上にいる敵の中から、最も遠い敵を優先して狙う。射程内は通常攻撃、射程外なら接近しながら1.5倍のダメージで突進する。直線上に敵がいなければ最も近い敵を通常攻撃する。"
    },
    new UnitStatusEntry {
      type = PieceType.Bishop,  pieceName = "ビショップ", maxHp = 800,  attack = 150, attackInterval = 1.8f, shopCost = 200, dropChance = 0.30f, qualityTier = 2, attackRange = 3.2f,
      abilityName = "聖なる援護",
      abilityDescription = "遠距離から敵を攻撃すると同時に、最もHP割合の低い味方を回復する。ただし近くに敵がいる間は集中力を乱され、攻撃・回復の効果が低下する。"
    },
    new UnitStatusEntry {
      type = PieceType.Queen,   pieceName = "クイーン",   maxHp = 1800, attack = 350, attackInterval = 2.2f, shopCost = 400, dropChance = 0.50f, qualityTier = 3, attackRange = 3.5f,
      abilityName = "王佐の魔弾",
      abilityDescription = "遠距離から魔法弾を放ち、着弾点周辺の敵をまとめて巻き込む。自チームのKingの近くにいるほど攻撃ダメージが上昇する。"
    },
    new UnitStatusEntry {
      type = PieceType.King,    pieceName = "キング",     maxHp = 3000, attack = 150, attackInterval = 2.0f, shopCost = 0,   dropChance = 0f,    qualityTier = 1, attackRange = 1.8f,
      abilityName = "旗印",
      abilityDescription = "ポーンと同じ動きで戦う。生存している間、常時、自チーム全員の攻撃力を上昇させ、受けるダメージを軽減する加護を与える。倒れても味方が残っていれば毎ウェーブ全回復して復活する。"
    },
    new UnitStatusEntry {
      type = PieceType.Paladin, pieceName = "パラディン", maxHp = 4000, attack = 450, attackInterval = 2.0f, shopCost = 0,   dropChance = 0f,    qualityTier = 1, attackRange = 1.8f,
      abilityName = "薙ぎ払い",
      abilityDescription = "射程内にいる敵をまとめて攻撃する範囲攻撃を行う。"
    },
    new UnitStatusEntry {
      type = PieceType.EliteCavalier, pieceName = "精鋭騎兵", maxHp = 1600, attack = 280, attackInterval = 1.9f,
      shopCost = 0, dropChance = 0f, qualityTier = 1, attackRange = 1.8f,
      abilityName = "騎兵強襲",
      abilityDescription = "射程外の敵には、ナイトと同じL字ジャンプで背後を取り2倍のクリティカルダメージ" +
        "（ただし跳躍方向の隣接マスに駒がいると足を封じられジャンプできない）。射程内の敵には、" +
        "ポーンと同じ要領で斜めから1.5倍のダメージを与える。ポーンとナイトが融合した精鋭。"
    },
  };

  public UnitStatusEntry GetStats(PieceType type)
  {
    return units.Find(u => u.type == type);
  }
}
