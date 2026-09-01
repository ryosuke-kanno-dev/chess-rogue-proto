using System.Collections.Generic;
using UnityEngine;

// ステップ24: 敵ウェーブ構成のデータ化。
// Wave 1〜10はここで定義したデータを使用し、Wave 11以降（エンドレスモード）は
// DebugGameManager側の既存ロジック（wavesリストに該当エントリが無い場合のフォールバック）で処理する。
//
// 課題3【初期配置エリアの制限】: ここで指定するgridZは、
// DebugGameManager.SpawnEnemyAtGrid() 側で最終的に「盤面奥側2列
// （GameConfigSO.enemyBackRowDepthで設定。既定ではZ=6〜7）」の範囲へ
// 強制的にクランプされるため、仮にInspectorで範囲外の値（Z=4など）を設定しても
// 実際にプレイヤー側へ侵入したスポーンにはならない（＝多重の安全策）。
// とはいえ意図と実際の見た目を一致させるため、デフォルト値自体もZ=6〜7に収まるよう修正済み。
[CreateAssetMenu(fileName = "EnemyWaveData", menuName = "Game/Enemy Wave Data")]
public class EnemyWaveDataSO : ScriptableObject
{
  [System.Serializable]
  public class EnemySpawnEntry
  {
    public PieceType type;
    [Tooltip("盤面グリッドX (0〜7)")]
    public int gridX;
    [Tooltip("盤面グリッドZ。実際の生成時は盤面奥側2列（既定ではZ=6〜7）へ自動的にクランプされる")]
    public int gridZ;
  }

  [System.Serializable]
  public class WaveEntry
  {
    [Tooltip("何ウェーブ目の構成か (1〜10)")]
    public int waveNumber;
    public List<EnemySpawnEntry> spawns = new List<EnemySpawnEntry>();
    [Tooltip("このウェーブの敵全員に適用するAI行動パターン。未設定(null)の場合はバランス型として扱う")]
    public AIBehaviorDataSO aiBehavior;
  }

  // 既存のハードコード値（Wave1〜3固有）をそのまま初期値として持たせてある。
  // Wave4〜10は「共通のdefault構成」だが、Inspector上でWave単位に個別編集できるよう、
  // それぞれ独立した WaveEntry / List<EnemySpawnEntry> インスタンスとして生成する（後述）。
  public List<WaveEntry> waves = BuildDefaultWaves();

  static List<WaveEntry> BuildDefaultWaves()
  {
    List<WaveEntry> result = new List<WaveEntry>
    {
      new WaveEntry
      {
        waveNumber = 1,
        spawns = new List<EnemySpawnEntry>
        {
          // 課題3修正: 旧gridZ=5（奥から3列目）→ 6（奥側2列の範囲内）へ修正
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 2, gridZ = 6 },
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 5, gridZ = 6 },
        }
      },
      new WaveEntry
      {
        waveNumber = 2,
        spawns = new List<EnemySpawnEntry>
        {
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 2, gridZ = 6 },
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 5, gridZ = 6 },
          new EnemySpawnEntry { type = PieceType.Knight, gridX = 4, gridZ = 7 },
        }
      },
      new WaveEntry
      {
        waveNumber = 3,
        spawns = new List<EnemySpawnEntry>
        {
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 1, gridZ = 6 },
          new EnemySpawnEntry { type = PieceType.Pawn, gridX = 6, gridZ = 6 },
          new EnemySpawnEntry { type = PieceType.Knight, gridX = 3, gridZ = 7 },
          new EnemySpawnEntry { type = PieceType.Bishop, gridX = 5, gridZ = 7 },
        }
      },
    };

    // ステップ25【不具合予防】: Wave4〜10は、ループの毎回の反復で DefaultComposition() を個別に
    // 呼び出す。DefaultComposition() は呼ばれるたびに new List<EnemySpawnEntry> { new EnemySpawnEntry{...}, ... }
    // という「完全に独立した新しいインスタンス」を返すため、Inspector上でWave4のスポーン内容を
    // 編集してもWave5〜10には一切影響しない。
    // （NGパターン: static readonly なリストをキャッシュして使い回す、DefaultComposition()の戻り値を
    // 変数に入れて複数のWaveEntryへ使い回す、等は絶対に行わないこと）
    for (int wave = 4; wave <= 10; wave++)
    {
      result.Add(new WaveEntry { waveNumber = wave, spawns = DefaultComposition() });
    }

    return result;
  }

  // 呼び出すたびに必ず新しい List<EnemySpawnEntry> ＋ 新しい EnemySpawnEntry 群を生成して返す。
  // 他のWaveEntryとインスタンスを共有することは無い。
  static List<EnemySpawnEntry> DefaultComposition()
  {
    return new List<EnemySpawnEntry>
    {
      // 課題3修正: 旧gridZ=4/5（奥から3〜4列目）→ 6/7（奥側2列の範囲内）へ修正
      new EnemySpawnEntry { type = PieceType.Rook, gridX = 2, gridZ = 6 },
      new EnemySpawnEntry { type = PieceType.Queen, gridX = 4, gridZ = 6 },
      new EnemySpawnEntry { type = PieceType.Knight, gridX = 5, gridZ = 7 },
      new EnemySpawnEntry { type = PieceType.Pawn, gridX = 3, gridZ = 7 },
    };
  }

  public WaveEntry GetWave(int waveNumber)
  {
    return waves.Find(w => w.waveNumber == waveNumber);
  }
}
