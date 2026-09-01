using UnityEngine;
using UnityEngine.UI;

public class PieceHealthBar : MonoBehaviour
{
  private PieceData targetPiece;
  private GameObject canvasObj;      // HPバー本体（背景＋Fill）
  private GameObject textCanvasObj;  // ステップ10: HP数値テキスト（現在HP / 最大HP）
  private Image hpFillImage;
  private Text hpText;
  private Camera mainCamera;

  // ステップ10: HP割合に応じた色のしきい値
  private const float HighHpRatio = 0.6f;
  private const float LowHpRatio = 0.3f;

  void Start()
  {
    targetPiece = GetComponent<PieceData>();
    mainCamera = Camera.main;

    CreateHealthBarUI();
    CreateHealthTextUI();
    UpdateHealthBar(); // 初期表示を更新
  }

  void CreateHealthBarUI()
  {
    canvasObj = new GameObject("HPBar_Canvas");
    canvasObj.transform.SetParent(transform);
    canvasObj.transform.localPosition = new Vector3(0, 1.2f, 0);

    Canvas canvas = canvasObj.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.WorldSpace;

    RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
    canvasRect.sizeDelta = new Vector2(1.2f, 0.2f);
    canvasObj.transform.localScale = new Vector3(1, 1, 1);

    // 絶対にエラーの出ない1x1の白スプライトを動的に生成
    Texture2D whiteTexture = Texture2D.whiteTexture;
    Sprite whiteSprite = Sprite.Create(
      whiteTexture,
      new Rect(0, 0, whiteTexture.width, whiteTexture.height),
      new Vector2(0.5f, 0.5f)
    );

    // 背景（黒）
    GameObject bgObj = new GameObject("HP_BG");
    bgObj.transform.SetParent(canvasObj.transform, false);
    Image bgImage = bgObj.AddComponent<Image>();
    bgImage.sprite = whiteSprite;
    bgImage.color = Color.black;

    RectTransform bgRect = bgObj.GetComponent<RectTransform>();
    bgRect.anchorMin = Vector2.zero;
    bgRect.anchorMax = Vector2.one;
    bgRect.sizeDelta = Vector2.zero;

    // HPゲージ（ステップ10: 色は残量に応じてUpdateHealthBarが動的に設定する）
    GameObject fillObj = new GameObject("HP_Fill");
    fillObj.transform.SetParent(canvasObj.transform, false);
    hpFillImage = fillObj.AddComponent<Image>();
    hpFillImage.sprite = whiteSprite;
    hpFillImage.type = Image.Type.Filled;
    hpFillImage.fillMethod = Image.FillMethod.Horizontal;
    hpFillImage.color = new Color(0.25f, 0.9f, 0.25f); // 初期は満タン想定の緑（直後にUpdateHealthBarで正しい色へ更新）

    RectTransform fillRect = fillObj.GetComponent<RectTransform>();
    fillRect.anchorMin = Vector2.zero;
    fillRect.anchorMax = Vector2.one;
    fillRect.sizeDelta = Vector2.zero;
  }

  // ステップ10: 「現在HP / 最大HP」テキストをバーの少し上に表示するための専用Canvas
  void CreateHealthTextUI()
  {
    textCanvasObj = new GameObject("HP_Text_Canvas");
    textCanvasObj.transform.SetParent(transform);
    textCanvasObj.transform.localPosition = new Vector3(0, 1.45f, 0);

    Canvas textCanvas = textCanvasObj.AddComponent<Canvas>();
    textCanvas.renderMode = RenderMode.WorldSpace;

    RectTransform textCanvasRect = textCanvasObj.GetComponent<RectTransform>();
    textCanvasRect.sizeDelta = new Vector2(240f, 80f);
    textCanvasObj.transform.localScale = Vector3.one * 0.005f; // バーとほぼ同じ幅（1.2ワールド単位）に収まるよう調整

    GameObject textObj = new GameObject("HP_Text");
    textObj.transform.SetParent(textCanvasObj.transform, false);

    hpText = textObj.AddComponent<Text>();
    hpText.alignment = TextAnchor.MiddleCenter;
    hpText.font = GetDefaultFont();
    hpText.fontSize = 46;
    hpText.fontStyle = FontStyle.Bold;
    hpText.horizontalOverflow = HorizontalWrapMode.Overflow;
    hpText.verticalOverflow = VerticalWrapMode.Overflow;

    RectTransform textRect = textObj.GetComponent<RectTransform>();
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.sizeDelta = Vector2.zero;

    // 視認性向上のための黒アウトライン（標準UIコンポーネントのみ・外部アセット不要）
    Outline outline = textObj.AddComponent<Outline>();
    outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
    outline.effectDistance = new Vector2(1.5f, -1.5f);
  }

  Font GetDefaultFont()
  {
    // Unity標準の組み込みフォントのみを使用（外部アセット不要）。バージョン差異に対して二段構えでフォールバック。
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    if (font == null)
    {
      font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
    return font;
  }

  void Update()
  {
    if (targetPiece == null || canvasObj == null) return;

    // 毎フレームHPの割合を監視してバー・数値・色・表示状態を更新
    // （ダメージ/ヒール/復活/装備/HP引き継ぎなど、あらゆる変化経路を取りこぼさないための保険）
    UpdateHealthBar();

    // ステップ10: カメラ参照が失われていた場合の防御的再取得＋ビルボード処理（バー・数値の両方）
    if (mainCamera == null) mainCamera = Camera.main;
    if (mainCamera != null)
    {
      canvasObj.transform.rotation = mainCamera.transform.rotation;
      if (textCanvasObj != null)
      {
        textCanvasObj.transform.rotation = mainCamera.transform.rotation;
      }
    }
  }

  // 外部から明示的にHPバーを更新するための関数
  public void UpdateHealthBar()
  {
    if (targetPiece == null || hpFillImage == null) return;
    if (targetPiece.maxHp <= 0) return;

    bool isAlive = targetPiece.currentHp > 0;

    // 課題5【復活時のHPバー非表示バグ修正】: 以前はUpdate()内で「HP<=0ならcanvasObj/textCanvasObjを
    // SetActive(false)」するだけの片方向処理しか無く、一度非表示になったHPバーは、
    // その後キングやプレイヤー駒がCleanUpBattlefield()やTryRebirth()で復活してcurrentHpが
    // 再び0より大きくなっても、自動的には再表示されなかった
    // （親である駒本体をSetActive(true)しても、子オブジェクト自身のactiveSelfがfalseのままだと戻らないため）。
    // ここで「生存していれば必ずSetActive(true)、死亡していればSetActive(false)」を毎回明示的に保証することで、
    // 死亡→復活のどの経路を辿っても、HPバーの表示状態が必ずcurrentHpの実態と一致するようにする。
    if (canvasObj != null && canvasObj.activeSelf != isAlive) canvasObj.SetActive(isAlive);
    if (textCanvasObj != null && textCanvasObj.activeSelf != isAlive) textCanvasObj.SetActive(isAlive);

    if (!isAlive) return; // 死亡中（非表示中）は数値更新不要

    float hpRatio = Mathf.Clamp01((float)targetPiece.currentHp / (float)targetPiece.maxHp);
    hpFillImage.fillAmount = hpRatio;

    // ステップ10: 残量に応じた動的カラー（60%以上=緑 / 30〜60%=黄 / 30%未満=赤）
    Color statusColor = GetColorForRatio(hpRatio);
    hpFillImage.color = statusColor;

    if (hpText != null)
    {
      hpText.text = $"{targetPiece.currentHp} / {targetPiece.maxHp}";
      hpText.color = statusColor;
    }
  }

  Color GetColorForRatio(float ratio)
  {
    if (ratio >= HighHpRatio) return new Color(0.25f, 0.9f, 0.25f);  // 緑（安全）
    if (ratio >= LowHpRatio) return new Color(1f, 0.85f, 0.15f);     // 黄（注意）
    return new Color(1f, 0.2f, 0.2f);                                  // 赤（ピンチ）
  }
}
