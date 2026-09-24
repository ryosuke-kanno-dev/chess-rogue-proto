using UnityEngine;

// ハイスコアの永続保存（PlayerPrefs）を担当する静的ユーティリティ
public static class ScoreManager
{
  private const string HighScoreKey = "AutoChess_HighScore";
  private const string HighScoreWaveKey = "AutoChess_HighScoreWave";

  public static int GetHighScore()
  {
    return PlayerPrefs.GetInt(HighScoreKey, 0);
  }

  public static int GetHighScoreWave()
  {
    return PlayerPrefs.GetInt(HighScoreWaveKey, 0);
  }

  // 新しいスコアが既存のハイスコアを上回っていれば保存し、更新の有無をboolで返す
  public static bool SaveHighScoreIfBetter(int score, int wave)
  {
    int currentHigh = GetHighScore();

    if (score > currentHigh)
    {
      PlayerPrefs.SetInt(HighScoreKey, score);
      PlayerPrefs.SetInt(HighScoreWaveKey, wave);
      PlayerPrefs.Save();
      return true;
    }

    return false;
  }

  public static void ResetHighScore()
  {
    PlayerPrefs.DeleteKey(HighScoreKey);
    PlayerPrefs.DeleteKey(HighScoreWaveKey);
    PlayerPrefs.Save();
    Debug.Log("🗑️ ハイスコアをリセットしました。");
  }
}
