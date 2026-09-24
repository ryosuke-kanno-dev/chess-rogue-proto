using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 課題【タイトル画面・シーン遷移】: タイトルシーン専用の最小限のコントローラー。
// 「スタート」ボタン押下でMainGameシーンを読み込むだけの、見た目には一切こだわらない実装。
// デザインが固まった段階で、別途装飾・演出等を追加していく想定。
public class TitleSceneController : MonoBehaviour
{
  [Tooltip("「スタート」ボタン")]
  [SerializeField] private Button startButton;
  [SerializeField] private Button settingsButton;

  void Start()
  {
    if (startButton != null)
    {
      startButton.onClick.AddListener(OnStartClicked);
    }

    // 課題【設定画面】: Titleシーンには排他制御対象の他パネルが存在しないため、
    // SettingsPanelUI.Instanceを直接呼び出す
    if (settingsButton != null)
    {
      settingsButton.onClick.AddListener(() =>
      {
        if (SettingsPanelUI.Instance != null) SettingsPanelUI.Instance.Show();
      });
    }

    // 課題【サウンドシステム: BGM接続】: タイトル画面のBGMを再生する
    if (AudioManager.Instance != null) AudioManager.Instance.PlayTitleBgm();
  }

  void OnStartClicked()
  {
    SceneManager.LoadScene("MainGame");
  }
}
