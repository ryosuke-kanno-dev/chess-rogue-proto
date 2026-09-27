using UnityEngine;
using Chebyss.Battle;

// 動作確認専用の一時スクリプト。PieceData.CreateSnapshot()・入力層の実装後は削除してよい。
public class BattleSmokeTest : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;

    void Start()
    {
        Debug.Log("[SmokeTest] 配置待ちの駒: " + string.Join(", ", bootstrapper.Placement.PendingPieceTypes));

        // 配置フェーズ：自陣（y<3）の3マスに仮配置する
        Log("Knight配置", bootstrapper.Placement.PlacePendingPiece(PieceType.Knight, new Vector2Int(3, 1)));
        Log("Rook配置",   bootstrapper.Placement.PlacePendingPiece(PieceType.Rook,   new Vector2Int(4, 1)));
        Log("Bishop配置", bootstrapper.Placement.PlacePendingPiece(PieceType.Bishop, new Vector2Int(5, 1)));

        Debug.Log("[SmokeTest] 配置完了: " + bootstrapper.Placement.IsComplete);

        bootstrapper.StartBattlePhase();
        Debug.Log("[SmokeTest] バトル開始。現在のフェーズ: " + bootstrapper.TurnController.CurrentPhase);

        var movable = bootstrapper.TurnController.GetMovableTiles(new Vector2Int(3, 1));
        Debug.Log("[SmokeTest] Knightの移動可能マス数: " + movable.Count + " → " + string.Join(" / ", movable));

        bootstrapper.TurnController.EndPlayerTurn();
        Debug.Log("[SmokeTest] ターン終了後のフェーズ: " + bootstrapper.TurnController.CurrentPhase
            + " / 現在のターン数: " + bootstrapper.Board.CurrentTurn);

        var testPieceGO = GameObject.Find("TestKnightPiece");
        if (testPieceGO == null)
        {
            Debug.LogError("[SmokeTest] シーン上にGameObject「TestKnightPiece」が見つかりません。PieceDataをアタッチしたGameObjectをこの名前で作成してください。");
        }
        else
        {
            var pieceData = testPieceGO.GetComponent<PieceData>();
            if (pieceData == null)
            {
                Debug.LogError("[SmokeTest] TestKnightPieceにPieceDataコンポーネントがアタッチされていません。");
            }
            else
            {
                var snapshot = pieceData.CreateSnapshot(new Vector2Int(0, 0));
                Debug.Log($"[SmokeTest] CreateSnapshot確認: pieceType={snapshot.pieceType}, isPlayerSide={snapshot.isPlayerSide}, currentHp={snapshot.currentHp}, attack={snapshot.attack}");
            }
        }
    }

    private void Log(string label, PlacementResult result)
    {
        Debug.Log($"[SmokeTest] {label}: {result}");
    }
}
