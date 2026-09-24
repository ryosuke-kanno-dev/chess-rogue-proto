using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 課題【左右クリック分岐】: 装備/インベントリの各スロットを左クリックした際に、
// アイテム名+ステータス内訳をポップアップ表示する軽量コンポーネント。
// WaveChoiceModalController.cs 等と同じ構成（panelRoot + テキスト + 閉じるボタン）を踏襲し、
// TooltipUI.cs と同じくシングルトン参照（ItemDetailPopup.Instance）で他スクリプトから呼び出せるようにする。
public class ItemDetailPopup : MonoBehaviour
{
  [SerializeField] private GameObject panelRoot;
  [SerializeField] private TextMeshProUGUI nameText;
  [SerializeField] private TextMeshProUGUI statDetailText; // ステータス内訳を複数行で表示
  [SerializeField] private Button closeButton;

  public static ItemDetailPopup Instance { get; private set; }

  void Awake()
  {
    // 課題【自己参照バグの防止】: panelRootに自分自身が誤って割り当てられていないかを実行時に検出する。
    if (panelRoot == gameObject)
    {
      Debug.LogError($"🚨 {GetType().Name}（{gameObject.name}）: panelRootに自分自身が" +
        "割り当てられています。この状態でHide()すると、二度と表示に戻れなくなります。" +
        "panelRootには、必ず「子オブジェクト」を割り当ててください。");
    }

    if (Instance == null) Instance = this;
    else if (Instance != this) Destroy(gameObject);

    if (closeButton != null) closeButton.onClick.AddListener(Hide);
    if (panelRoot != null) panelRoot.SetActive(false);
  }

  public void Show(EquipmentInstance item)
  {
    if (item == null || panelRoot == null) return;

    if (nameText != null) nameText.text = item.itemName;

    if (statDetailText != null)
    {
      // 既存のPieceInspectPanelUI.RefreshEquippedSlots()内で行っている
      // EquipmentGenerator.FormatBonus(b) の組み立てと同じ要領で、item.bonusesを1行ずつ改行区切りでまとめる
      System.Text.StringBuilder sb = new System.Text.StringBuilder();
      foreach (var b in item.bonuses)
      {
        sb.AppendLine(EquipmentGenerator.FormatBonus(b));
      }
      statDetailText.text = sb.ToString();
    }

    panelRoot.SetActive(true);
  }

  public void Hide()
  {
    if (panelRoot != null) panelRoot.SetActive(false);
  }
}
