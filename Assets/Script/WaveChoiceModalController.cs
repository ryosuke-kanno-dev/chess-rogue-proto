using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ30: 3択フロア選択モーダルの自動制御。
// DebugGameManager.UI_IsWaveChoiceModalOpen() を毎フレーム監視し、開閉に応じてpanelRootを自動的に
// 表示/非表示にする。ボタンのラベルはDebugGameManager側のWaveChoiceOptionデータから自動生成するため、
// データ（弱敵/中敵/強敵の倍率・報酬）を変更してもこのスクリプトの修正は不要。
public class WaveChoiceModalController : MonoBehaviour
{
  [Header("パネル全体")]
  [Tooltip("3択モーダル表示中のみ表示するルート。未表示時は自動的にSetActive(false)になる")]
  [SerializeField] private GameObject panelRoot;

  [Header("選択ボタン（配列インデックス = WaveChoiceOptionのインデックスと対応。0/1/2）")]
  [SerializeField] private Button[] choiceButtons = new Button[3];
  [SerializeField] private TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[3];

  [Header("課題【AIパターンのSO管理化】")]
  [Tooltip("「今回のウェーブの敵の傾向」を示す固定のテキスト（3択どれを選んでも共通の内容）。未設定でも動作する")]
  [SerializeField] private TextMeshProUGUI enemyTrendText;

  private DebugGameManager gm;

  void Start()
  {
    gm = DebugGameManager.Instance;
    RegisterButtonEvents();

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  private void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    // 課題【AIパターンのSO管理化】: 以前はここでpanelRootの表示/非表示切り替えのみを行っており、
    // RefreshModal()（ボタンラベルの内容更新処理）自体はどこからも呼ばれていなかった
    // （静的な選択肢データを前提にしていたため実害は出ていなかったが、本来は毎フレーム最新化すべき箇所）。
    // 今回、敵の傾向テキストをここに追加するのに合わせて、RefreshModal()を正しくUpdate()から呼ぶよう修正した。
    RefreshModal();
  }
  // Start()で各ボタンのonClickを自動登録する（Inspector側のOnClick()設定は不要）
  void RegisterButtonEvents()
  {
    if (choiceButtons == null) return;

    for (int i = 0; i < choiceButtons.Length; i++)
    {
      if (choiceButtons[i] == null) continue;

      int captured = i; // クロージャ用にローカル変数へキャプチャ
      choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(captured));
    }
  }

  void RefreshModal()
  {
    bool isOpen = gm.UI_IsWaveChoiceModalOpen();

    if (panelRoot != null) panelRoot.SetActive(isOpen);
    if (!isOpen) return;

    WaveChoiceOption[] options = gm.UI_GetWaveChoiceOptions();
    if (options != null && choiceLabels != null)
    {
      for (int i = 0; i < choiceLabels.Length; i++)
      {
        bool hasOption = i < options.Length;

        if (choiceLabels[i] != null)
        {
          if (hasOption)
          {
            WaveChoiceOption option = options[i];
            choiceLabels[i].text = $"{option.label}\n敵倍率: {option.enemyStatMultiplier:0.0}x / 報酬: +{option.goldReward}G";
          }
          else
          {
            choiceLabels[i].text = "";
          }
        }

        if (choiceButtons != null && i < choiceButtons.Length && choiceButtons[i] != null)
        {
          choiceButtons[i].interactable = hasOption;
        }
      }
    }

    // 課題【AIパターンのSO管理化】: 3択のラベルとは別に、「今回のウェーブの敵の傾向」を示す固定のテキスト
    // （3択どれを選んでも共通の内容）を表示する。CurrentWaveAIBehaviorがnullの場合はバランス型として表示する。
    if (enemyTrendText != null)
    {
      AIBehaviorDataSO behavior = gm.CurrentWaveAIBehavior;
      if (behavior != null)
      {
        string desc = string.IsNullOrEmpty(behavior.patternDescription) ? "" : $" - {behavior.patternDescription}";
        enemyTrendText.text = $"敵の傾向: {behavior.patternName}{desc}";
      }
      else
      {
        enemyTrendText.text = "敵の傾向: バランス型";
      }
    }
  }

  void OnChoiceClicked(int index)
  {
    if (gm == null) return;

    // DebugGameManager.UI_SelectWaveChoice() 内でGold付与・倍率確定・showWaveChoiceModal=false までまとめて行われる
    gm.UI_SelectWaveChoice(index);
  }
}
