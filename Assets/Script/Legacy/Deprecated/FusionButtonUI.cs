using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【異種合成「精鋭騎兵」】: 融合ボタン1個分の表示を担当するコンポーネント。
// MergeButtonUI.cs と同じ構成（Initialize時のonClick登録・SetData）を踏襲した、独立した専用UI
// （MergeButtonUI自体には一切手を加えていない）。
public class FusionButtonUI : MonoBehaviour
{
  [Header("スロットUI参照")]
  [SerializeField] private Button fusionButton;
  [SerializeField] private TextMeshProUGUI labelText;

  private int currentRecipeIndex;
  private System.Action<int> onClicked;

  // FusionButtonsUI側から一度だけ呼び出し、クリック時のコールバックを登録する
  public void Initialize(System.Action<int> clickHandler)
  {
    onClicked = clickHandler;

    if (fusionButton == null)
    {
      Debug.LogWarning($"⚠️ FusionButtonUI（{gameObject.name}）: Fusion Button がInspectorで未設定です。");
      return;
    }

    fusionButton.onClick.RemoveListener(HandleClick);
    fusionButton.onClick.AddListener(HandleClick);
  }

  // 課題【異種合成「精鋭騎兵」】: isAvailable==falseのレシピはボタンをinteractable=falseにしつつ、
  // labelText自体は「（素材不足）」と分かる形で表示する
  public void SetData(DebugGameManager.FusionCandidateInfo info)
  {
    currentRecipeIndex = info.recipeIndex;
    gameObject.SetActive(true);

    if (labelText != null)
    {
      labelText.text = info.isAvailable ? info.recipeName : $"{info.recipeName}（素材不足）";
    }

    if (fusionButton != null)
    {
      fusionButton.interactable = info.isAvailable;
    }
  }

  public void Hide()
  {
    gameObject.SetActive(false);
  }

  void HandleClick()
  {
    onClicked?.Invoke(currentRecipeIndex);
  }
}
