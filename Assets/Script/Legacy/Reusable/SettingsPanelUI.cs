using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【設定画面】: Title・MainGame両シーンから開ける設定画面。
// PieceAIBehaviorSelectorModal.csと同じ「単体で完結するシングルトンポップアップ」の構成を踏襲する。
// 両シーンにそれぞれ独立したGameObjectとして配置する想定（DontDestroyOnLoadは使用しない。
// 両シーンとも同じSettingsManager経由でデータを共有するため、表示内容は自然に同期される）。
public class SettingsPanelUI : MonoBehaviour
{
  public static SettingsPanelUI Instance { get; private set; }
  public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

  [Header("パネル全体")]
  [SerializeField] private GameObject panelRoot;
  [SerializeField] private Button closeButton;

  [Header("音量（保存のみ。実際の反映は将来のサウンド実装時）")]
  [SerializeField] private Slider masterVolumeSlider;
  [SerializeField] private Slider bgmVolumeSlider;
  [SerializeField] private Slider seVolumeSlider;

  [Header("画面設定")]
  [SerializeField] private TMP_Dropdown resolutionDropdown;
  [SerializeField] private Toggle fullscreenToggle;

  private Resolution[] availableResolutions;
  private bool isInitializing; // 初期値セット中、onValueChangedの誤発火で保存が走らないようにするガード

  void Awake()
  {
    // 課題【自己参照バグの防止】
    if (panelRoot == gameObject)
    {
      Debug.LogError($"🚨 {GetType().Name}（{gameObject.name}）: panelRootに自分自身が" +
        "割り当てられています。この状態でHide()すると、二度と表示に戻れなくなります。" +
        "panelRootには、必ず「子オブジェクト」を割り当ててください。");
    }

    if (Instance == null) Instance = this;
    else if (Instance != this) Destroy(gameObject);
  }

  void Start()
  {
    if (closeButton != null) closeButton.onClick.AddListener(Hide);

    BuildResolutionDropdown();
    LoadIntoUI();

    if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
    if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
    if (seVolumeSlider != null) seVolumeSlider.onValueChanged.AddListener(OnSeVolumeChanged);
    if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  void BuildResolutionDropdown()
  {
    if (resolutionDropdown == null) return;

    // 課題【解像度ドロップダウンの重複解消】: Screen.resolutionsはリフレッシュレート違いの
    // 同一解像度（例: 640x480@60Hz, 640x480@59.94Hz等）を別エントリとして返すため、
    // 表示前に「幅・高さ」の組み合わせで重複排除する（リフレッシュレートの違いは
    // 今回のドロップダウンでは考慮しない。各組み合わせにつき最初に見つかった1件を採用する）。
    var uniqueResolutions = new System.Collections.Generic.List<Resolution>();
    var seenSizes = new System.Collections.Generic.HashSet<(int width, int height)>();

    foreach (var r in Screen.resolutions)
    {
      var key = (r.width, r.height);
      if (seenSizes.Contains(key)) continue;

      seenSizes.Add(key);
      uniqueResolutions.Add(r);
    }

    availableResolutions = uniqueResolutions.ToArray();

    resolutionDropdown.ClearOptions();

    var options = new System.Collections.Generic.List<string>();
    foreach (var r in availableResolutions)
    {
      options.Add($"{r.width} x {r.height}");
    }
    resolutionDropdown.AddOptions(options);
  }

  // 保存済みの値をUIへ反映し、解像度/フルスクリーンは実際の画面にも適用する
  void LoadIntoUI()
  {
    isInitializing = true;

    if (masterVolumeSlider != null) masterVolumeSlider.value = SettingsManager.GetMasterVolume();
    if (bgmVolumeSlider != null) bgmVolumeSlider.value = SettingsManager.GetBgmVolume();
    if (seVolumeSlider != null) seVolumeSlider.value = SettingsManager.GetSeVolume();

    bool fullscreen = SettingsManager.GetFullscreen();
    if (fullscreenToggle != null) fullscreenToggle.isOn = fullscreen;

    int savedIndex = SettingsManager.GetResolutionIndex();
    if (savedIndex >= 0 && availableResolutions != null && savedIndex < availableResolutions.Length)
    {
      if (resolutionDropdown != null) resolutionDropdown.value = savedIndex;
      var res = availableResolutions[savedIndex];
      Screen.SetResolution(res.width, res.height, fullscreen);
    }
    else
    {
      Screen.fullScreen = fullscreen;
    }

    isInitializing = false;
  }

  void OnMasterVolumeChanged(float value) { if (!isInitializing) SettingsManager.SetMasterVolume(value); }
  void OnBgmVolumeChanged(float value) { if (!isInitializing) SettingsManager.SetBgmVolume(value); }
  void OnSeVolumeChanged(float value) { if (!isInitializing) SettingsManager.SetSeVolume(value); }

  void OnResolutionChanged(int index)
  {
    if (isInitializing) return;
    if (availableResolutions == null || index < 0 || index >= availableResolutions.Length) return;

    var res = availableResolutions[index];
    bool fullscreen = fullscreenToggle != null && fullscreenToggle.isOn;
    Screen.SetResolution(res.width, res.height, fullscreen);
    SettingsManager.SetResolutionIndex(index);
  }

  void OnFullscreenChanged(bool isOn)
  {
    if (isInitializing) return;
    Screen.fullScreen = isOn;
    SettingsManager.SetFullscreen(isOn);
  }

  public void Show()
  {
    if (panelRoot != null) panelRoot.SetActive(true);
  }

  public void Hide()
  {
    if (panelRoot != null) panelRoot.SetActive(false);
  }
}
