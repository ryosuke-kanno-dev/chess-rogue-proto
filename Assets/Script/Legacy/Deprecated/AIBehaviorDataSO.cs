using UnityEngine;

// 課題【AIパターンのSO管理化】: 駒のターゲット選定ロジックの種類。
// 敵側（EnemyWaveDataSO.WaveEntry.aiBehavior、ウェーブ単位）・
// プレイヤー側（PieceData.aiBehavior、駒ごとの個別設定）の両方から共通で参照する。
public enum EnemyTargetingMode
{
  Balanced,          // バランス型：既存の「最も近い敵」ロジック
  WeakestFirst,      // 弱者優先型：HP割合が最も低い敵を狙う
  HighestValueFirst, // 本命特攻型：ショップコスト(shopCost)が最も高い敵を狙う
}

// 課題【AIパターンのSO管理化】: 1つのターゲット選定パターン（バランス型/弱者優先型/本命特攻型など）を表すSO。
// パターンごとに別々のスクリプトを用意するのではなく、このSOのアセットを複数作成し（Balanced用、
// WeakestFirst用、HighestValueFirst用の3種類など）、敵ウェーブ・プレイヤー駒の両方から同じアセットを
// 使い回す想定。実際のターゲット選定ロジックの実装本体はPieceAI.cs側にあり、
// このSOはあくまで「どのロジックを使うか」を指し示すデータ＋UI表示用のラベルを持つのみ。
[CreateAssetMenu(fileName = "AIBehaviorData", menuName = "Game/AI Behavior Data")]
public class AIBehaviorDataSO : ScriptableObject
{
  [Tooltip("UI表示用のパターン名（例: 「弱者優先型」）")]
  public string patternName;
  [Tooltip("UI表示用の説明文（例: 「HPが低い敵を優先して狙う」）")]
  [TextArea(2, 3)]
  public string patternDescription;
  [Tooltip("実際のターゲット選定ロジックの種類")]
  public EnemyTargetingMode targetingMode;
}
