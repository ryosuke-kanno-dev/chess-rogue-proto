using UnityEngine;

// 課題【設定画面】: 音量・画面設定の永続保存を担当する静的ユーティリティ。
// ScoreManager.cs と同じ設計パターン（static classによるPlayerPrefsラッパー）を踏襲する。
public static class SettingsManager
{
  private const string MasterVolumeKey = "Settings_MasterVolume";
  private const string BgmVolumeKey = "Settings_BgmVolume";
  private const string SeVolumeKey = "Settings_SeVolume";
  private const string ResolutionIndexKey = "Settings_ResolutionIndex";
  private const string FullscreenKey = "Settings_Fullscreen";

  public static float GetMasterVolume() => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
  public static void SetMasterVolume(float value)
  {
    PlayerPrefs.SetFloat(MasterVolumeKey, value);
    PlayerPrefs.Save();
    // 課題【サウンドシステム】: 「将来AudioMixer等を導入した際、ここで実際の音量へ反映する処理を
    // 追加する想定」としていた箇所の実装。AudioManager側でマスター×カテゴリ音量を再計算させる。
    if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumeSettings();
  }

  public static float GetBgmVolume() => PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
  public static void SetBgmVolume(float value)
  {
    PlayerPrefs.SetFloat(BgmVolumeKey, value);
    PlayerPrefs.Save();
    if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumeSettings();
  }

  public static float GetSeVolume() => PlayerPrefs.GetFloat(SeVolumeKey, 1f);
  public static void SetSeVolume(float value)
  {
    PlayerPrefs.SetFloat(SeVolumeKey, value);
    PlayerPrefs.Save();
    if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumeSettings();
  }

  // -1 = 未設定（起動時の現在解像度をそのまま使う）
  public static int GetResolutionIndex() => PlayerPrefs.GetInt(ResolutionIndexKey, -1);
  public static void SetResolutionIndex(int index) { PlayerPrefs.SetInt(ResolutionIndexKey, index); PlayerPrefs.Save(); }

  public static bool GetFullscreen() => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
  public static void SetFullscreen(bool value) { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); }
}
