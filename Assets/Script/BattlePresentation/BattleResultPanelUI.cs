using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Chebyss.Battle;

// 注意：このコンポーネントは、panelRoot（結果パネル）とは別の「常にアクティブなGameObject」
// （BattleResultCanvas）にアタッチすること。panelRoot自身にアタッチすると、Start()で
// SetActive(false)した時点でUpdate()が止まり、二度と表示されなくなる。
public class BattleResultPanelUI : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private Button retryButton;

    [Header("表示文言（フォント対応・多言語対応のためInspectorから変更可能にする）")]
    [SerializeField] private string victoryMessage = "勝利！";
    [SerializeField] private string defeatMessage = "チェックメイト";

    private BattlePhase lastPhase = BattlePhase.PlayerTurn;

    void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        var turnController = bootstrapper.TurnController;
        if (turnController == null) return;

        var phase = turnController.CurrentPhase;
        if (phase == lastPhase) return;
        lastPhase = phase;

        if (phase == BattlePhase.Victory) Show(victoryMessage);
        else if (phase == BattlePhase.Defeat) Show(defeatMessage);
    }

    private void Show(string message)
    {
        if (resultTitleText != null) resultTitleText.text = message;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void OnRetryClicked()
    {
        // 現在のシーンがBuild Profilesのシーンリストに追加されている必要がある
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
