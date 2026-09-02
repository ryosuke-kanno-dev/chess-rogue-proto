using System.Collections.Generic;
using UnityEngine;

// ステップ23: 合成ボタン群のUGUI化。
// 同時に複数の駒種が★1×3以上（または課題【★2→★3合成】で追加した★2×3以上）そろうケースに対応するため、
// slotPrefabを最大表示数ぶんだけ起動時に1度だけプールし（動的Instantiateはこの初期化時のみ）、
// 以後は対象がある分だけ表示する。
public class MergeButtonsUI : MonoBehaviour
{
  [Header("スロット構成")]
  [Tooltip("MergeButtonUIをアタッチしたプレハブ")]
  [SerializeField] private GameObject slotPrefab;
  [Tooltip("スロットをまとめて配置する親（Vertical Layout Group推奨）")]
  [SerializeField] private Transform slotContainer;
  [Tooltip("同時に表示しうる合成ボタンの最大数（駒種の数だけあれば十分。既定6）")]
  [SerializeField] private int maxSlots = 6;

  private DebugGameManager gm;
  private readonly List<MergeButtonUI> slots = new List<MergeButtonUI>();

  void Start()
  {
    gm = DebugGameManager.Instance;
    BuildSlots();
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    RefreshSlots();
  }

  void BuildSlots()
  {
    if (slotPrefab == null || slotContainer == null) return;

    for (int i = 0; i < maxSlots; i++)
    {
      GameObject obj = Instantiate(slotPrefab, slotContainer);
      obj.name = $"MergeButtonSlot_{i}";

      MergeButtonUI slot = obj.GetComponent<MergeButtonUI>();
      if (slot == null)
      {
        Debug.LogWarning("⚠️ MergeButtonsUI: slotPrefabにMergeButtonUIがアタッチされていません。");
        continue;
      }

      slot.Initialize(OnMergeButtonClicked);
      slot.Hide();
      slots.Add(slot);
    }
  }

  void RefreshSlots()
  {
    // 準備フェーズ以外、またはUGUIの成長モーダルが開いている間は合成ボタンを出さない
    // 課題【フェーズ2: 操作ブロック】: 合成/融合の手動選択モード中は、別の合成/融合ボタン群を非表示にする
    // （選択モード中に別の合成を開始してしまい、選択状態が上書きされる事故を防ぐため）
    bool isPrepPhase = !gm.isBattleStarted && !gm.isGameOver && !gm.UI_IsGrowthModalOpen() && !gm.UI_IsSelectionModeActive();

    if (!isPrepPhase)
    {
      HideAllSlots();
      return;
    }

    // 課題【★2→★3合成の育成履歴分岐システム】: UI_GetMergeCandidates()の戻り値の型が
    // List<KeyValuePair<PieceType,int>> から List<MergeCandidateInfo>（fromRank付き）へ変更されたことに対応
    List<DebugGameManager.MergeCandidateInfo> candidates = gm.UI_GetMergeCandidates();

    for (int i = 0; i < slots.Count; i++)
    {
      if (i < candidates.Count)
      {
        slots[i].SetData(candidates[i].type, candidates[i].fromRank, candidates[i].count);
      }
      else
      {
        slots[i].Hide();
      }
    }
  }

  void HideAllSlots()
  {
    for (int i = 0; i < slots.Count; i++)
    {
      slots[i].Hide();
    }
  }

  void OnMergeButtonClicked(PieceType type, int fromRank)
  {
    if (gm == null) return;

    // 課題【合成/融合の手動選択モード】: 以前はここでgm.UI_ExecuteMerge(type, fromRank)を呼び、
    // 対象の駒を自動選択して即座に合成/進化を実行していたが、
    // 「対象条件に合う駒の中からプレイヤー自身が誰を使うか選び、確定ボタンを押すまで実行されない」
    // という2段階の操作へ変更するため、ここではDebugGameManagerの選択モードを開始するだけに留める。
    // 実際の合成/進化の実行は、MergeSelectionListUIの確定ボタン（gm.UI_ConfirmSelection()）が担う。
    gm.UI_StartMergeSelection(type, fromRank);
  }
}
