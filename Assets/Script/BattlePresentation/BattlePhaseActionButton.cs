using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Chebyss.Battle;

// 「配置完了」「ターン終了」を1つのボタンにまとめ、状況に応じてラベル・有効状態・
// 表示を切り替える。このコンポーネント自身は常にアクティブなGameObject
// （PhaseActionButtonController）に付け、ボタン本体（buttonRoot）の方を
// 表示・非表示する。BattleResultPanelUIで経験した「自分自身を非表示にすると
// 二度とUpdate()が走らなくなる」問題を、最初から避けるための構成。
public class BattlePhaseActionButton : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;
    [SerializeField] private GameObject buttonRoot;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI buttonLabel;

    void Start()
    {
        if (actionButton != null) actionButton.onClick.AddListener(() => bootstrapper.AdvancePhase());
    }

    void Update()
    {
        if (bootstrapper == null || buttonRoot == null) return;

        if (bootstrapper.TurnController == null)
        {
            // 配置フェーズ中：全駒配置済みのときだけ押せる
            buttonRoot.SetActive(true);
            if (buttonLabel != null) buttonLabel.text = "配置完了";
            if (actionButton != null)
                actionButton.interactable = bootstrapper.Placement != null && bootstrapper.Placement.IsComplete;
            return;
        }

        // バトル中：プレイヤーターンのときだけ表示する（敵ターン・勝敗確定後は隠す）
        bool isPlayerTurn = bootstrapper.TurnController.CurrentPhase == BattlePhase.PlayerTurn;
        buttonRoot.SetActive(isPlayerTurn);
        if (isPlayerTurn && buttonLabel != null) buttonLabel.text = "ターン終了";
    }
}
