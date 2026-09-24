using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ23: 合成ボタン1個分の表示を担当するコンポーネント。
// ★1が3体以上（または課題【★2→★3合成】で追加した★2が3体以上）そろっている駒種ごとに、
// このプレハブが1つ表示される。
public class MergeButtonUI : MonoBehaviour
{
  [Header("スロットUI参照")]
  [SerializeField] private Button mergeButton;
  [SerializeField] private TextMeshProUGUI labelText;

  private PieceType currentType;
  private int currentFromRank;
  private System.Action<PieceType, int> onClicked;

  // MergeButtonsUI側から一度だけ呼び出し、クリック時のコールバックを登録する
  public void Initialize(System.Action<PieceType, int> clickHandler)
  {
    onClicked = clickHandler;

    if (mergeButton == null)
    {
      Debug.LogWarning($"⚠️ MergeButtonUI（{gameObject.name}）: Merge Button がInspectorで未設定です。");
      return;
    }

    mergeButton.onClick.RemoveListener(HandleClick);
    mergeButton.onClick.AddListener(HandleClick);
  }

  // 課題【★2→★3合成の育成履歴分岐システム】: fromRankを追加し、ラベル文言をfromRankに応じて出し分ける
  public void SetData(PieceType type, int fromRank, int count)
  {
    currentType = type;
    currentFromRank = fromRank;
    gameObject.SetActive(true);

    if (labelText != null)
    {
      string label = fromRank == 1
        ? $"合成: ★1→★2 {type} ({count})"
        : $"合成: ★2→★3 {type} ({count})";
      labelText.text = label;
    }
  }

  public void Hide()
  {
    gameObject.SetActive(false);
  }

  void HandleClick()
  {
    onClicked?.Invoke(currentType, currentFromRank);
  }
}
