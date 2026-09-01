using UnityEngine;
using TMPro;

// 課題【駒特性説明のホバーツールチップ】:
// 盤面上の駒(3D, PieceTooltipTrigger)・ショップボタン(UGUI, ShopSlotTooltipTrigger)の
// どちらからも同じ実装を呼び出すための、単一の軽量ツールチップコンポーネント。
// 表示ロジック（テキストの組み立て・空文字列時のガード・マウス追従）をここに集約することで、
// 2つのトリガー側に表示ロジックが重複しないようにする。
//
// 既存のUIManager/PieceInspectPanelUIと同じく、DebugGameManager.Instance相当のシングルトンパターン
// （public static TooltipUI Instance）で実装し、他スクリプトからは TooltipUI.Instance.Show(...) /
// TooltipUI.Instance.Hide() の形で呼び出せるようにする。
//
// 【表示内容のスコープ】要件通り「駒名＋特性説明文」のみを表示する。HP/攻撃力等の数値は
// 既存のPieceInspectPanelUIやショップの価格表示に任せ、ここでは一切扱わない。
public class TooltipUI : MonoBehaviour
{
  public static TooltipUI Instance { get; private set; }

  [Header("参照（Inspectorで設定）")]
  [Tooltip("ツールチップ全体のルートGameObject。非表示時はこれをSetActive(false)にする")]
  [SerializeField] private GameObject panelRoot;
  [Tooltip("駒名（abilityName）を表示するテキスト")]
  [SerializeField] private TextMeshProUGUI nameText;
  [Tooltip("特性説明文（abilityDescription）を表示するテキスト")]
  [SerializeField] private TextMeshProUGUI descriptionText;
  [Tooltip("ツールチップパネル自身のRectTransform（マウス追従の移動対象）。未設定ならpanelRootのRectTransformを使う")]
  [SerializeField] private RectTransform panelRectTransform;
  [Tooltip("このツールチップが乗っているCanvasのRectTransform（スクリーン座標→ローカル座標の変換に使用）")]
  [SerializeField] private RectTransform canvasRectTransform;
  [Tooltip("このCanvasを描画しているカメラ。Canvasのレンダーモードが Screen Space - Overlay の場合はnullのままでよい")]
  [SerializeField] private Camera canvasCamera;

  [Header("表示位置の調整")]
  [Tooltip("カーソル位置からのオフセット（スクリーンピクセル単位）。カーソルにパネルが重なって隠れてしまうのを防ぐ")]
  [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

  void Awake()
  {
    if (Instance == null) Instance = this;
    else if (Instance != this) Destroy(gameObject);

    if (panelRectTransform == null && panelRoot != null)
    {
      panelRectTransform = panelRoot.GetComponent<RectTransform>();
    }

    Hide();
  }

  // 課題【空文字列時のガード】: abilityName/abilityDescriptionが未設定（空文字列）の駒種については、
  // 「説明未設定」の表示にはせず、そもそもツールチップ自体を出さない（要件の「防御的に実装」の指示に基づく判断）。
  // 呼び出し側（PieceTooltipTrigger / ShopSlotTooltipTrigger）は、このメソッドの戻り値を見て
  // 表示できたかどうかを判定する必要はなく、単に呼ぶだけでよい（内部で自動的に無視される）。
  public void Show(string abilityName, string abilityDescription, Vector2 screenPosition)
  {
    if (panelRoot == null || nameText == null || descriptionText == null) return;

    bool hasContent = !string.IsNullOrEmpty(abilityName) || !string.IsNullOrEmpty(abilityDescription);
    if (!hasContent)
    {
      Hide();
      return;
    }

    nameText.text = abilityName;
    descriptionText.text = abilityDescription;

    panelRoot.SetActive(true);
    UpdatePosition(screenPosition);
  }

  // 既に表示中のツールチップの追従位置だけを更新したい場合に使う（Show()を毎フレーム呼んでもよいが、
  // テキストを再設定しない分、こちらの方がわずかに軽い）
  public void UpdatePosition(Vector2 screenPosition)
  {
    if (panelRectTransform == null) return;

    Vector2 targetScreenPos = screenPosition + cursorOffset;

    if (canvasRectTransform == null)
    {
      // Canvas参照が無い場合の簡易フォールバック: そのままスクリーン座標をローカル座標として扱う
      panelRectTransform.position = targetScreenPos;
      return;
    }

    Vector2 localPoint;
    bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
      canvasRectTransform, targetScreenPos, canvasCamera, out localPoint);

    if (converted)
    {
      panelRectTransform.localPosition = localPoint;
    }
  }

  public void Hide()
  {
    if (panelRoot != null) panelRoot.SetActive(false);
  }
}
