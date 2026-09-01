using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【合成/融合の手動選択モード】: 一覧リスト（MergeSelectionListUI）内の1候補駒ぶんの表示・クリック検知を担当する。
// 盤面上の駒への直接クリック（PieceDraggable→DebugGameManager.UI_ToggleSelectionForPiece）とは別経路だが、
// 最終的に呼ぶAPIは同じ（DebugGameManager.UI_ToggleSelectionForPiece）であり、選択状態を共有する。
public class MergeSelectionRowUI : MonoBehaviour
{
  [Header("行UI参照")]
  [SerializeField] private Button rowButton;
  [SerializeField] private Image backgroundImage; // 選択状態を色で示す（未選択/選択済み）
  [SerializeField] private TextMeshProUGUI nameText; // 例:「ナイト ★1」

  [Header("選択状態の色")]
  [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.15f);
  [SerializeField] private Color selectedColor = new Color(0.3f, 1f, 0.4f, 0.6f);

  private PieceData currentPiece;
  private System.Action<PieceData> onClicked;

  // MergeSelectionListUI側から一度だけ呼び出し、クリック時のコールバックを登録する
  public void Initialize(System.Action<PieceData> clickHandler)
  {
    onClicked = clickHandler;

    if (rowButton == null)
    {
      Debug.LogWarning($"⚠️ MergeSelectionRowUI（{gameObject.name}）: Row Button がInspectorで未設定です。");
      return;
    }

    rowButton.onClick.RemoveListener(HandleClick);
    rowButton.onClick.AddListener(HandleClick);
  }

  public void SetData(PieceData piece, bool isSelected)
  {
    currentPiece = piece;
    gameObject.SetActive(true);

    if (nameText != null && piece != null)
    {
      nameText.text = $"{piece.pieceName} ★{piece.rank}";
    }

    if (backgroundImage != null)
    {
      backgroundImage.color = isSelected ? selectedColor : unselectedColor;
    }
  }

  public void Hide()
  {
    currentPiece = null;
    gameObject.SetActive(false);
  }

  void HandleClick()
  {
    if (currentPiece == null) return;
    onClicked?.Invoke(currentPiece);
  }
}
