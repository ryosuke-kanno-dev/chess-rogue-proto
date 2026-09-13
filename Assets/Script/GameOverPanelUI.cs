using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【ゲームオーバー画面のUGUI化】: 旧OnGUI版DrawGameOverPanel()の表示内容をそのまま再現した、
// UGUI版のゲームオーバーパネル。WaveChoiceModalController.cs等と同じ構成
// （panelRoot監視 + 複数テキスト + ボタン）を踏襲する。
public class GameOverPanelUI : MonoBehaviour
{
  [SerializeField] private GameObject panelRoot;
  [SerializeField] private TextMeshProUGUI waveText;         // 例:「到達ウェーブ: 5」
  [SerializeField] private TextMeshProUGUI killText;         // 例:「総撃破数: 32」
  [SerializeField] private TextMeshProUGUI goldHpText;       // 例:「最終ゴールド: 1200   残HP: 0」
  [SerializeField] private TextMeshProUGUI scoreText;        // 例:「⭐ 最終スコア: 8400 pt」
  [SerializeField] private TextMeshProUGUI highScoreOrRecordText; // New Record! または 現在のハイスコア
  [SerializeField] private Button retryButton;
  [SerializeField] private Button titleButton;

  private DebugGameManager gm;

  void Awake()
  {
    // 課題【自己参照バグの防止】: panelRootに自分自身が誤って割り当てられていないかを実行時に検出する。
    if (panelRoot == gameObject)
    {
      Debug.LogError($"🚨 {GetType().Name}（{gameObject.name}）: panelRootに自分自身が" +
        "割り当てられています。この状態でHide()すると、二度と表示に戻れなくなります。" +
        "panelRootには、必ず「子オブジェクト」を割り当ててください。");
    }

    gm = DebugGameManager.Instance;
  }

  void Start()
  {
    if (retryButton != null) retryButton.onClick.AddListener(() => gm.ResetScene());
    if (titleButton != null) titleButton.onClick.AddListener(() => gm.ReturnToTitle());

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    bool isOpen = gm.isGameOver;
    if (panelRoot != null) panelRoot.SetActive(isOpen);
    if (!isOpen) return;

    RefreshPanel();
  }

  void RefreshPanel()
  {
    if (waveText != null)
    {
      waveText.text = gm.isEndlessMode
        ? $"🌊 到達ウェーブ: {gm.currentWave} (ENDLESS)"
        : $"到達ウェーブ: {gm.currentWave}";
    }
    if (killText != null) killText.text = $"総撃破数: {gm.totalEnemiesDefeated}";
    if (goldHpText != null) goldHpText.text = $"最終ゴールド: {gm.gold}   残HP: {gm.playerHp}";
    if (scoreText != null) scoreText.text = $"⭐ 最終スコア: {gm.UI_GetFinalScore()} pt";

    if (highScoreOrRecordText != null)
    {
      highScoreOrRecordText.text = gm.UI_IsNewHighScore()
        ? "New Record!"
        : $"🏆 ハイスコア: {ScoreManager.GetHighScore()} pt (Wave {ScoreManager.GetHighScoreWave()})";
    }
  }
}
