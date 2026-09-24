using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ23: 進化ボーナス3択カード1枚分の表示を担当するコンポーネント。
public class GrowthCardUI : MonoBehaviour
{
  [Header("カードUI参照")]
  [SerializeField] private Button cardButton;
  [SerializeField] private TextMeshProUGUI titleText;
  [SerializeField] private TextMeshProUGUI descText;

  private GrowthType currentType;
  private System.Action<GrowthType> onClicked;

  // GrowthModalUI側から一度だけ呼び出し、クリック時のコールバックを登録する
  public void Initialize(System.Action<GrowthType> clickHandler)
  {
    onClicked = clickHandler;

    if (cardButton == null)
    {
      Debug.LogWarning($"⚠️ GrowthCardUI（{gameObject.name}）: Card Button がInspectorで未設定です。");
      return;
    }

    cardButton.onClick.RemoveListener(HandleClick);
    cardButton.onClick.AddListener(HandleClick);
  }

  public void SetData(GrowthType type, string title, string desc)
  {
    currentType = type;
    gameObject.SetActive(true);

    if (titleText != null) titleText.text = title;
    if (descText != null) descText.text = desc;
  }

  // ステップ25: 候補が3件未満の場合に、余ったカードへ前回の内容が残ったまま表示されるのを防ぐ
  public void Hide()
  {
    gameObject.SetActive(false);
  }

  void HandleClick()
  {
    onClicked?.Invoke(currentType);
  }
}
