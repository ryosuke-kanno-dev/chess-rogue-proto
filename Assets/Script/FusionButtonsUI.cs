using System.Collections.Generic;
using UnityEngine;

// 課題【異種合成「精鋭騎兵」】: 融合ボタン群のUGUI化。
// MergeButtonsUI.cs と同じ構成（スロットプール・Initialize時のonClick登録・可変長対応・
// isPrepPhase判定）を踏襲した、独立した専用UIコンポーネント
// （MergeButtonsUI自体には一切手を加えていない）。
public class FusionButtonsUI : MonoBehaviour
{
  [Header("スロット構成")]
  [Tooltip("FusionButtonUIをアタッチしたプレハブ")]
  [SerializeField] private GameObject slotPrefab;
  [Tooltip("スロットをまとめて配置する親（Vertical Layout Group推奨）")]
  [SerializeField] private Transform slotContainer;
  [Tooltip("同時に表示しうる融合レシピの最大数（既定4）")]
  [SerializeField] private int maxSlots = 4;

  private DebugGameManager gm;
  private readonly List<FusionButtonUI> slots = new List<FusionButtonUI>();

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
      obj.name = $"FusionButtonSlot_{i}";

      FusionButtonUI slot = obj.GetComponent<FusionButtonUI>();
      if (slot == null)
      {
        Debug.LogWarning("⚠️ FusionButtonsUI: slotPrefabにFusionButtonUIがアタッチされていません。");
        continue;
      }

      slot.Initialize(OnFusionButtonClicked);
      slot.Hide();
      slots.Add(slot);
    }
  }

  void RefreshSlots()
  {
    // 課題【異種合成「精鋭騎兵」】: 表示条件はMergeButtonsUI.RefreshSlots()のisPrepPhase判定をそのまま踏襲する
    bool isPrepPhase = !gm.isBattleStarted && !gm.isGameOver && !gm.UI_IsGrowthModalOpen();

    if (!isPrepPhase)
    {
      HideAllSlots();
      return;
    }

    List<DebugGameManager.FusionCandidateInfo> candidates = gm.UI_GetFusionCandidates();

    for (int i = 0; i < slots.Count; i++)
    {
      if (i < candidates.Count)
      {
        slots[i].SetData(candidates[i]);
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

  void OnFusionButtonClicked(int recipeIndex)
  {
    if (gm == null) return;

    // 課題【合成/融合の手動選択モード】: 以前はここでgm.UI_ExecuteFusion(recipeIndex)を呼び、
    // 素材を自動収集して即座に融合を実行していたが、
    // 「対象条件に合う駒の中からプレイヤー自身が誰を使うか選び、確定ボタンを押すまで実行されない」
    // という2段階の操作へ変更するため、ここではDebugGameManagerの選択モードを開始するだけに留める。
    // 実際の融合の実行は、MergeSelectionListUIの確定ボタン（gm.UI_ConfirmSelection()）が担う。
    gm.UI_StartFusionSelection(recipeIndex);
  }
}
