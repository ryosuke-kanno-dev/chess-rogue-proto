using System.Collections.Generic;
using UnityEngine;

// 課題1対応: DebugGameManagerや各スクリプトに散らばっていた「ゲーム全体」の設定値
// （初期Gold、回復コスト、盤面設定、ウェーブ/フロア補正値、経済・スコア関連の定数など）を
// 1つのScriptableObjectへ集約する。
//
// 駒個別のステータス・コスト・ドロップ率等は UnitStatusDataSO（PieceDataSOの役割）側に集約し、
// こちらはあくまで「駒に依存しないゲーム全体設定」のみを扱う。
//
// 【設計方針】
// 既存のDebugGameManager側フィールドは削除せず、フォールバック用のデフォルト値として残す。
// gameConfigがInspectorで未設定の場合や、リストに該当エントリが無い場合は、
// 既存のハードコード値へ自動的にフォールバックする（他のSOと同じ設計パターンに統一）。
// ステップ29: 3択フロア選択システムの1選択肢分のデータ。
// 元々DebugGameManager.cs内にあった定義をGameConfigSOへ移設した。
[System.Serializable]
public class WaveChoiceOption
{
  public string label;               // 表示名（例: 弱敵/中敵/強敵）
  public float enemyStatMultiplier;  // 敵のHP/攻撃力に乗算する倍率
  public int goldReward;             // 選択時に即座に得られるGold報酬
  public float dropRateMultiplier = 1f; // 装備ドロップ率への乗算倍率（強敵選択時のドロップ率UP用）
}

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config")]
public class GameConfigSO : ScriptableObject
{
  [Header("経済システム")]
  [Tooltip("プレイヤーの初期所持Gold")]
  public int initialGold = 400;

  [Header("フェーズ & ステージ状態")]
  [Tooltip("プレイヤーの初期HP（敗北ダメージを受ける基準値）")]
  public int initialPlayerHp = 300;

  [Header("回復・リロールコスト")]
  [Tooltip("負傷した味方駒をGoldで全回復させる際のコスト")]
  public int healCost = 2000;
  [Tooltip("ショップの有償リロール（並び替え）のコスト")]
  public int rerollCost = 200;

  [Header("インベントリ")]
  [Tooltip("装備インベントリの最大所持数")]
  public int maxInventorySlots = 8;

  [Header("ベンチ設定（盤面グリッドとは完全に独立）")]
  [Tooltip("ベンチのスロット数。盤面のBoardWidth/BoardDepthとは無関係に、この数だけ横一列に配置される")]
  public int benchSlotCount = 8;
  [Tooltip("盤面の手前端（Z軸マイナス側）からベンチ列までの間隔（マス単位）")]
  public float benchGapFromBoard = 1.0f;

  [Header("盤面設定")]
  [Tooltip("盤面の横幅（グリッドX方向のマス数）")]
  public int boardWidth = 8;
  [Tooltip("盤面の縦幅（グリッドZ方向のマス数）")]
  public int boardDepth = 8;
  [Tooltip("プレイヤー駒が配置可能な自陣の行数（盤面手前からこの行数まで）")]
  public int playerFrontRowDepth = 2;
  [Tooltip("課題3: 敵駒がスポーンし得る「盤面奥側」の行数。\n" +
           "例: boardDepth=8, enemyBackRowDepth=2 の場合、Z座標が (8-2)=6 〜 (8-1)=7 の範囲にのみ敵は生成される")]
  public int enemyBackRowDepth = 2;

  [Header("キング育成・スキルツリー")]
  [Tooltip("指揮のオーラ Lv毎の全体攻撃力バフ量")]
  public int skillAuraAttackPerLevel = 50;
  [Tooltip("富の知識 Lv毎の追加ゴールド量（ウェーブクリア時）")]
  public int skillEconomyGoldPerLevel = 200;
  [Tooltip("王の加護 Lv毎の耐久（HP）加算量")]
  public int skillBarrierHpPerLevel = 300;

  [Header("ウェーブクリア報酬")]
  [Tooltip("ウェーブクリア時の基本ボーナスGold（キング育成の富の知識で加算される前のベース値）")]
  public int waveBaseGoldReward = 500;

  [Header("課題3: コイン獲得・収支バランス（一元化）")]
  [Tooltip("敵を1体撃破するごとに得られるGold。0の場合は敵撃破報酬なし（既定は現状維持のため0）")]
  public int goldPerEnemyKill = 0;
  [Tooltip("装備インベントリが満杯の時に自動売却して得られるGold（レアリティ別）")]
  public int sellValueCommon = 100;
  public int sellValueRare = 200;
  public int sellValueEpic = 400;
  public int sellValueLegendary = 700;
  [Tooltip("OnGUIデバッグパネルの「+Gold」ボタン1回あたりの付与量")]
  public int debugGoldGrantAmount = 10;

  [Header("課題1: 撃破時の生存・復活システム")]
  [Tooltip("プレイヤー駒が撃破された際、真の死亡（墓地送り・装備清算）ではなく" +
           "「瀕死のまま生存(HP1)」になる確率（0〜1）。1-この値が真の死亡率になる")]
  [Range(0f, 1f)]
  public float playerNearDeathSurviveChance = 0.7f;

  [Header("エンドレスモード")]
  [Tooltip("エンドレスモードへ移行するウェーブ数（このウェーブ到達でエンドレス開始）")]
  public int endlessStartWave = 11;
  [Tooltip("エンドレスモード中、1ウェーブごとに敵ステータスへ乗算される倍率")]
  public float endlessScalingRate = 1.18f;

  [Header("スコア計算")]
  public int scorePerWave = 1000;
  public int scorePerKill = 100;
  public int scorePerGold = 10;
  public int scorePerHp = 5;

  [Header("ステップ29: 3択フロア選択システム")]
  [Tooltip("ウェーブ開始前に提示する3択（弱敵/中敵/強敵）。ここで敵ステータス倍率・Gold報酬・ドロップ率倍率を一元管理する")]
  public List<WaveChoiceOption> waveChoiceOptions = new List<WaveChoiceOption>
  {
    new WaveChoiceOption { label = "弱敵", enemyStatMultiplier = 0.8f, goldReward = 2000, dropRateMultiplier = 1.0f },
    new WaveChoiceOption { label = "中敵", enemyStatMultiplier = 1.0f, goldReward = 4000, dropRateMultiplier = 1.0f },
    new WaveChoiceOption { label = "強敵", enemyStatMultiplier = 1.3f, goldReward = 8000, dropRateMultiplier = 1.5f },
  };

  [Header("課題4: プレイヤー駒に設定可能なAIパターン一覧")]
  [Tooltip("PieceAIBehaviorSelectorModalの選択肢として表示するAIBehaviorDataSOの一覧")]
  public AIBehaviorDataSO[] playerSelectableAIBehaviors;
}
