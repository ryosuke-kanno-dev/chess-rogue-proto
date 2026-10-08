using UnityEngine;
using TMPro;

// 画面上にBoardState.CurrentTurnを表示する。値が変わったときだけ更新する
// （BattleResultPanelUIのlastPhase比較と同じ考え方）。
public class TurnCounterDisplay : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;
    [SerializeField] private TextMeshProUGUI turnText;

    private int lastSeenTurn = -1;

    void Update()
    {
        if (bootstrapper.Board == null || turnText == null) return;

        int currentTurn = bootstrapper.Board.CurrentTurn;
        if (currentTurn == lastSeenTurn) return;

        lastSeenTurn = currentTurn;
        turnText.text = $"Turn {currentTurn}";
    }
}
