using UnityEngine;
using UnityEngine.UI;

// ダメージ/回復ポップアップの種別（表示色・文字サイズの打ち分けに使用）
public enum DamagePopupType
{
  Normal,   // 通常ダメージ（赤）
  Critical, // クリティカル/特殊ダメージ（黄・大きめ）
  Heal      // 回復（緑）
}

// 戦闘ダメージ・回復量をワールド空間に浮かび上がらせて自動消滅するポップアップ。
// DamagePopup.Create(...) を呼ぶだけで生成〜アニメーション〜破棄までが完結する（マネージャー不要）。
public class DamagePopup : MonoBehaviour
{
  private const float BaseDuration = 0.7f; // 約0.6〜0.8秒でフェードアウト
  private const float MoveDistance = 0.6f; // 上方向への移動量

  private Text label;
  private Camera mainCamera;
  private float timer = 0f;
  private float duration;
  private Vector3 startPos;
  private Vector3 endPos;
  private Color baseColor;

  // 外部（PieceData等）から呼び出す生成用の静的メソッド
  public static void Create(Vector3 worldPosition, string text, DamagePopupType type)
  {
    // ステップ9: 連続ダメージでテキストが重ならないよう、発生位置に左右のランダムオフセットを付与
    float offsetX = Random.Range(-0.35f, 0.35f);
    float offsetZ = Random.Range(-0.15f, 0.15f);
    Vector3 spawnPos = worldPosition + new Vector3(offsetX, 0f, offsetZ);

    GameObject obj = new GameObject("DamagePopup");
    obj.transform.position = spawnPos;

    DamagePopup popup = obj.AddComponent<DamagePopup>();
    popup.Initialize(text, type);
  }

  void Initialize(string text, DamagePopupType type)
  {
    mainCamera = Camera.main;

    duration = BaseDuration + Random.Range(-0.05f, 0.1f); // 0.65〜0.8秒程度にばらつかせる
    startPos = transform.position;
    endPos = startPos + new Vector3(Random.Range(-0.15f, 0.15f), MoveDistance, 0f);

    BuildUI(text, type);
  }

  void BuildUI(string text, DamagePopupType type)
  {
    Canvas canvas = gameObject.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.WorldSpace;

    RectTransform canvasRect = GetComponent<RectTransform>();
    canvasRect.sizeDelta = new Vector2(300f, 120f);
    transform.localScale = Vector3.one * 0.01f;

    GameObject textObj = new GameObject("Text");
    textObj.transform.SetParent(transform, false);

    label = textObj.AddComponent<Text>();
    label.text = text;
    label.alignment = TextAnchor.MiddleCenter;
    label.font = GetDefaultFont();
    label.horizontalOverflow = HorizontalWrapMode.Overflow;
    label.verticalOverflow = VerticalWrapMode.Overflow;

    RectTransform textRect = textObj.GetComponent<RectTransform>();
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.sizeDelta = Vector2.zero;

    switch (type)
    {
      case DamagePopupType.Critical:
        baseColor = new Color(1f, 0.85f, 0.15f); // 黄色・大きめ
        label.fontSize = 64;
        label.fontStyle = FontStyle.Bold;
        break;

      case DamagePopupType.Heal:
        baseColor = new Color(0.3f, 1f, 0.45f); // 緑色
        label.fontSize = 46;
        label.fontStyle = FontStyle.Bold;
        break;

      case DamagePopupType.Normal:
      default:
        baseColor = new Color(1f, 0.25f, 0.2f); // 赤色
        label.fontSize = 46;
        label.fontStyle = FontStyle.Bold;
        break;
    }

    label.color = baseColor;

    // 視認性を上げるための黒アウトライン（標準UIコンポーネントのみ・外部アセット不要）
    Outline outline = textObj.AddComponent<Outline>();
    outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
    outline.effectDistance = new Vector2(2f, -2f);
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
    timer += Time.deltaTime;

    if (timer >= duration)
    {
      Destroy(gameObject);
      return;
    }

    float t = timer / duration;

    // イーズアウトしながら上昇
    float easedT = 1f - Mathf.Pow(1f - t, 2f);
    transform.position = Vector3.Lerp(startPos, endPos, easedT);

    // フェードアウト
    if (label != null)
    {
      Color c = baseColor;
      c.a = 1f - t;
      label.color = c;
    }

    // ビルボード（常にカメラの方を向く）
    if (mainCamera == null) mainCamera = Camera.main;
    if (mainCamera != null)
    {
      transform.rotation = mainCamera.transform.rotation;
    }
  }
}
