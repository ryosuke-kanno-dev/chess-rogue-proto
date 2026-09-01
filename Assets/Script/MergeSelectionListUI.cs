using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【合成/融合の手動選択モード】: 「対象条件に合う駒の中からプレイヤー自身が誰を使うか選び、
// 確定ボタンを押すまで実行されない」という2段階操作のうち、一覧リストからの選択を担当するUI。
// 盤面上の駒への直接クリック（PieceDraggable経由）とこのリストのチェックは、どちらも
// DebugGameManager.UI_ToggleSelectionForPiece(piece) という同じAPIを呼ぶため、選択状態を共有する
// （このリストで選んだ駒が盤上でハイライトされ、逆に盤上でクリックした駒がこのリストにも反映される）。
public class MergeSelectionListUI : MonoBehaviour
{
  [Header("パネル全体")]
  [Tooltip("選択モード中のみ表示するルート。DebugGameManager.UI_IsSelectionModeActive()と同期する")]
  [SerializeField] private GameObject panelRoot;

  [Header("行スロット構成")]
  [Tooltip("MergeSelectionRowUIをアタッチしたプレハブ")]
  [SerializeField] private GameObject rowPrefab;
  [Tooltip("行をまとめて配置する親（Vertical Layout Group推奨）")]
  [SerializeField] private Transform rowContainer;
  [Tooltip("同時に表示しうる候補駒の最大数（盤上・ベンチ全体で該当駒種が何体いても対応できるよう、余裕を持たせておく）")]
  [SerializeField] private int maxRows = 20;

  [Header("進捗・操作")]
  [Tooltip("「ポーン ★1: 1/1」のような、条件ごとの進捗をまとめて表示するテキスト")]
  [SerializeField] private TextMeshProUGUI progressText;
  [Tooltip("必要数が全て揃っている時のみinteractable=trueになる確定ボタン")]
  [SerializeField] private Button confirmButton;
  [Tooltip("何も実行せず選択モードを終了する中断ボタン")]
  [SerializeField] private Button cancelButton;

  private DebugGameManager gm;
  private readonly List<MergeSelectionRowUI> rows = new List<MergeSelectionRowUI>();

  void Start()
  {
    gm = DebugGameManager.Instance;
    BuildRows();

    if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    bool isActive = gm.UI_IsSelectionModeActive();
    if (panelRoot != null) panelRoot.SetActive(isActive);
    if (!isActive) return;

    RefreshRows();
    RefreshProgressText();

    if (confirmButton != null) confirmButton.interactable = gm.UI_IsSelectionComplete();
  }

  void BuildRows()
  {
    if (rowPrefab == null || rowContainer == null) return;

    for (int i = 0; i < maxRows; i++)
    {
      GameObject obj = Instantiate(rowPrefab, rowContainer);
      obj.name = $"MergeSelectionRow_{i}";

      MergeSelectionRowUI row = obj.GetComponent<MergeSelectionRowUI>();
      if (row == null)
      {
        Debug.LogWarning("⚠️ MergeSelectionListUI: rowPrefabにMergeSelectionRowUIがアタッチされていません。");
        continue;
      }

      row.Initialize(OnRowClicked);
      row.Hide();
      rows.Add(row);
    }
  }

  void RefreshRows()
  {
    List<PieceData> candidates = gm.UI_GetSelectionCandidates();

    for (int i = 0; i < rows.Count; i++)
    {
      if (i < candidates.Count)
      {
        PieceData piece = candidates[i];
        rows[i].SetData(piece, gm.UI_IsPieceSelected(piece));
      }
      else
      {
        rows[i].Hide();
      }
    }
  }

  void RefreshProgressText()
  {
    if (progressText == null) return;

    List<(PieceType type, int fromRank, int current, int required)> progress = gm.UI_GetSelectionProgress();

    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    for (int i = 0; i < progress.Count; i++)
    {
      var p = progress[i];
      if (i > 0) sb.Append("   ");
      sb.Append($"{p.type} ★{p.fromRank}: {p.current}/{p.required}");
    }
    progressText.text = sb.ToString();
  }

  // 一覧リストからの選択（盤面クリックと同じAPIを呼ぶ）
  void OnRowClicked(PieceData piece)
  {
    if (gm == null) return;
    gm.UI_ToggleSelectionForPiece(piece);
  }

  void OnConfirmClicked()
  {
    if (gm == null) return;
    gm.UI_ConfirmSelection();
  }

  void OnCancelClicked()
  {
    if (gm == null) return;
    gm.UI_CancelSelection();
  }
}
