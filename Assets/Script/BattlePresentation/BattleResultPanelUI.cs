using System.Text;
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
    [SerializeField] private TextMeshProUGUI dropListText;

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

        if (phase == BattlePhase.Victory)
        {
            if (bootstrapper.Loadout != null)
            {
                // 勝利時は結果パネルではなく、全画面の装備画面を開く
                bootstrapper.PrepareNextFloor();
                bootstrapper.Loadout.Open(turnController.LootLedger.Collected);
            }
            else
            {
                Show(victoryMessage);
                RefreshDropList(turnController);
            }
        }
        else if (phase == BattlePhase.Defeat)
        {
            Show(defeatMessage);
            if (dropListText != null) dropListText.text = ""; // 敗北時はドロップを回収しないため表示しない
        }
    }

    // 「[Rare] 鋼の指輪  ATK+120 HP+600」をレアリティ色付きで1行ずつ表示する
    private void RefreshDropList(BattleTurnController turnController)
    {
        if (dropListText == null) return;

        var sb = new StringBuilder();
        foreach (var item in turnController.LootLedger.Collected)
        {
            string hex = ColorUtility.ToHtmlStringRGB(BoardDropMarker.GetRarityColor(item.rarity));

            var bonusText = new StringBuilder();
            if (item.bonuses != null)
            {
                foreach (var bonus in item.bonuses)
                {
                    if (bonusText.Length > 0) bonusText.Append(' ');
                    // ボーナスの途中で折り返さない（ボーナス同士の区切りの空白では折り返してよい）
                    bonusText.Append("<nobr>").Append(BattleEquipmentGenerator.FormatBonus(bonus)).Append("</nobr>");
                }
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"<color=#{hex}>[{item.rarity}] {item.itemName}  {bonusText}</color>");
        }

        dropListText.text = sb.ToString();
    }

    private void Show(string message)
    {
        if (resultTitleText != null) resultTitleText.text = message;
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    private void OnRetryClicked()
    {
        // 現在のシーンがBuild Profilesのシーンリストに追加されている必要がある
        RunState.Reset(); // 敗北後のリトライはランを最初からやり直す
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
