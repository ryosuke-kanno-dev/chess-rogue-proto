using UnityEngine;
using TMPro;

// ステップ23: 進化ボーナス3択モーダルのUGUI化。
// DebugGameManager.UI_IsGrowthModalOpen() を毎フレーム監視し、開閉に応じてpanelRootを自動的に表示/非表示にする。
// カード3枚は起動時に1度だけ生成し（動的Instantiateはこの初期化時のみ）、以後は内容だけ差し替える。
public class GrowthModalUI : MonoBehaviour
{
  [Header("パネル全体")]
  [Tooltip("成長モーダル表示中のみ表示するルート。未表示時は自動的にSetActive(false)になる")]
  [SerializeField] private GameObject panelRoot;
  [SerializeField] private TextMeshProUGUI titleText; // 例:「★2 進化ボーナス！【○○】」

  [Header("カード構成")]
  [Tooltip("GrowthCardUIをアタッチしたカードのプレハブ")]
  [SerializeField] private GameObject cardPrefab;
  [SerializeField] private Transform cardContainer;

  private DebugGameManager gm;
  private GrowthCardUI[] cards;

  void Awake()
  {
    // 課題【自己参照バグの防止】: panelRootに自分自身（このスクリプトがアタッチされているGameObject）が
    // 誤って割り当てられていないかを実行時に検出する。自己参照のまま非表示化すると、このGameObject自体の
    // Update()が二度と呼ばれなくなり、二度と復帰できなくなるため、致命的な設定ミスとしてConsole上に警告を出す。
    if (panelRoot == gameObject)
    {
      Debug.LogError($"🚨 {GetType().Name}（{gameObject.name}）: panelRootに自分自身が" +
        "割り当てられています。この状態でHide()すると、二度と表示に戻れなくなります。" +
        "panelRootには、必ず「子オブジェクト」を割り当ててください。");
    }

    // 課題【初期化タイミングの堅牢化】: DebugGameManager.Instance自体への参照取得は、
    // Start()より確実に早いAwake()へ移動する（BuildCards()等、DebugGameManagerが完全に
    // 初期化済みであることに依存する処理はStart()のまま残す）。
    gm = DebugGameManager.Instance;
  }

  void Start()
  {
    BuildCards();

    if (panelRoot != null) panelRoot.SetActive(false);
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    RefreshModal();
  }

  // 3択カードを起動時に1度だけ生成する（選択肢はGrowthTypeの4種から毎回3つ抽選されるため、枚数は常に3で固定）
  void BuildCards()
  {
    if (cardPrefab == null || cardContainer == null) return;

    cards = new GrowthCardUI[3];
    for (int i = 0; i < 3; i++)
    {
      GameObject obj = Instantiate(cardPrefab, cardContainer);
      obj.name = $"GrowthCard_{i}";

      GrowthCardUI card = obj.GetComponent<GrowthCardUI>();
      if (card == null)
      {
        Debug.LogWarning("⚠️ GrowthModalUI: cardPrefabにGrowthCardUIがアタッチされていません。");
        continue;
      }

      card.Initialize(OnCardClicked);
      cards[i] = card;
    }
  }

  void RefreshModal()
  {
    bool isOpen = gm.UI_IsGrowthModalOpen();

    if (panelRoot != null) panelRoot.SetActive(isOpen);
    if (!isOpen) return;

    if (titleText != null)
    {
      titleText.text = $"★2 進化ボーナス！【{gm.UI_GetEvolvingPieceName()}】";
    }

    if (cards == null) return;

    GrowthType[] options = gm.UI_GetGrowthOptions();
    for (int i = 0; i < cards.Length; i++)
    {
      if (cards[i] == null) continue;

      if (i < options.Length)
      {
        gm.UI_GetGrowthOptionLabel(options[i], out string title, out string desc);
        cards[i].SetData(options[i], title, desc);
      }
      else
      {
        // ステップ25: 候補が3件未満の場合、余ったカードは前回の内容が残らないよう非表示にする
        cards[i].Hide();
      }
    }
  }

  void OnCardClicked(GrowthType type)
  {
    if (gm == null) return;
    gm.UI_ApplyGrowthChoice(type);
  }
}
