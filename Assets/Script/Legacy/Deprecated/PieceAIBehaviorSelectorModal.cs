using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【AIパターンのSO管理化】: プレイヤー駒に対して、個別のAI行動パターン（バランス型/弱者優先型/本命特攻型など）を
// 設定するための軽量な選択ポップアップ。WaveChoiceModalController.cs と同じ構成
// （panelRoot + 複数ボタン + ラベル配列）を踏襲し、選択肢の数はDebugGameManager.Instance.GameConfig.
// playerSelectableAIBehaviors の実際の要素数に応じて可変長対応する
// （ボタン数と選択肢数が一致しない場合も安全に動作するよう、WaveChoiceModalController.RefreshModal()と
// 同様の「hasOptionによる有効/無効切り替え」の書き方を踏襲している）。
public class PieceAIBehaviorSelectorModal : MonoBehaviour
{
  // 課題【UIの排他制御】: 他のポップアップ（TooltipUI、ItemDetailPopup）と同じシングルトン参照パターンを踏襲し、
  // 開閉状態（IsOpen）を外部（DebugGameManager等）から把握・操作できるようにする
  public static PieceAIBehaviorSelectorModal Instance { get; private set; }
  public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

  [Header("パネル全体")]
  [Tooltip("このポップアップ表示中のみ表示するルート。未表示時は自動的にSetActive(false)になる")]
  [SerializeField] private GameObject panelRoot;

  [Header("選択ボタン（配列インデックス = GameConfig.playerSelectableAIBehaviorsのインデックスと対応）")]
  [SerializeField] private Button[] choiceButtons;
  [SerializeField] private TextMeshProUGUI[] choiceLabels;

  [Header("閉じるボタン")]
  [Tooltip("何も選ばずにポップアップを閉じるためのボタン")]
  [SerializeField] private Button closeButton;

  // 課題【AIパターンのSO管理化】: Show()で渡された「今まさに設定しようとしている対象の駒」を保持する
  private PieceData targetPiece;

  void Awake()
  {
    // 課題【自己参照バグの防止】: panelRootに自分自身が誤って割り当てられていないかを実行時に検出する。
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
    RegisterButtonEvents();
    if (panelRoot != null) panelRoot.SetActive(false);
  }

  // Start()で各ボタンのonClickを自動登録する（Inspector側のOnClick()設定は不要）
  void RegisterButtonEvents()
  {
    if (choiceButtons != null)
    {
      for (int i = 0; i < choiceButtons.Length; i++)
      {
        if (choiceButtons[i] == null) continue;

        int captured = i; // クロージャ用にローカル変数へキャプチャ
        choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(captured));
      }
    }

    if (closeButton != null)
    {
      closeButton.onClick.AddListener(Hide);
    }
  }

  // 課題【AIパターンのSO管理化】: 対象の駒を保持しつつパネルを表示し、選択肢を最新化する
  public void Show(PieceData piece)
  {
    targetPiece = piece;
    if (panelRoot != null) panelRoot.SetActive(true);

    RefreshChoices();
  }

  public void Hide()
  {
    targetPiece = null;
    if (panelRoot != null) panelRoot.SetActive(false);
  }

  // DebugGameManager.Instance.GameConfig.playerSelectableAIBehaviors の内容でボタンラベルを動的に生成する。
  // パターン数がボタン数と異なる場合も、hasOptionの判定で安全に動作する
  // （選択肢が足りない分のボタンは非活性化・ラベル空欄にするだけで、配列外参照は起きない）。
  void RefreshChoices()
  {
    if (choiceLabels == null) return;

    AIBehaviorDataSO[] options = GetAvailableOptions();

    for (int i = 0; i < choiceLabels.Length; i++)
    {
      bool hasOption = options != null && i < options.Length && options[i] != null;

      if (choiceLabels[i] != null)
      {
        if (hasOption)
        {
          AIBehaviorDataSO option = options[i];
          choiceLabels[i].text = string.IsNullOrEmpty(option.patternDescription)
            ? option.patternName
            : $"{option.patternName}\n{option.patternDescription}";
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

  AIBehaviorDataSO[] GetAvailableOptions()
  {
    if (DebugGameManager.Instance == null) return null;
    if (DebugGameManager.Instance.GameConfig == null) return null;
    return DebugGameManager.Instance.GameConfig.playerSelectableAIBehaviors;
  }

  // ボタン押下時: targetPiece.aiBehavior に選んだAIBehaviorDataSOを設定し、パネルを閉じる
  void OnChoiceClicked(int index)
  {
    if (targetPiece == null) { Hide(); return; }

    AIBehaviorDataSO[] options = GetAvailableOptions();
    if (options != null && index >= 0 && index < options.Length)
    {
      targetPiece.aiBehavior = options[index];
    }

    Hide();
  }
}
