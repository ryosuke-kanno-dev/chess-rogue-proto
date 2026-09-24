using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Chebyss.Battle;

public class BattleResultPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private Button retryButton;

    private BattleTurnController turnController;
    private BattlePhase lastPhase = BattlePhase.PlayerTurn;

    public void Initialize(BattleTurnController controller)
    {
        turnController = controller;
    }

    void Start()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (turnController == null) return;

        var phase = turnController.CurrentPhase;
        if (phase == lastPhase) return;
        lastPhase = phase;

        if (phase == BattlePhase.Victory) Show("勝利！");
        else if (phase == BattlePhase.Defeat) Show("チェックメイト");
    }

    private void Show(string title)
    {
        if (resultTitleText != null) resultTitleText.text = title;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void OnRetryClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
