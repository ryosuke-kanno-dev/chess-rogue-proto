using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Chebyss.Battle;

// ラン結果画面（ランクリア／敗北）。UIはすべて実行時にコードで生成する（シーン編集不要）。
public class RunEndPanelUI : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;

    private GameObject canvasObject;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI summaryText;
    private Button newRunButton;
    private int openedFrame = -1;
    private bool pendingSelect;

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;
        BuildUI();
    }

    public void Open(bool cleared, string title)
    {
        titleText.text = title;
        titleText.color = cleared ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);

        var sb = new StringBuilder();
        sb.Append($"到達階: {RunState.CurrentFloor}F / {bootstrapper.TotalFloors}F\n");
        sb.Append($"クリアした階: {RunState.FloorsCleared}\n");
        sb.Append($"倒した敵: {RunState.EnemiesDefeated}\n");
        sb.Append($"獲得した装備: {RunState.ItemsCollected}\n");
        sb.Append($"失った駒: {RunState.PiecesLost}");
        summaryText.text = sb.ToString();

        if (EventSystem.current == null)
            Debug.LogWarning("[RunEndPanelUI] シーンに EventSystem がありません。ボタンが反応しません。");

        canvasObject.SetActive(true);

        // 開いたフレームのキー入力（直前の決定キー・Spaceなど）で即座にリスタートしないよう、
        // ボタンの選択とSpaceの受け付けは次のフレームからにする
        openedFrame = Time.frameCount;
        pendingSelect = true;
    }

    void Update()
    {
        if (canvasObject == null || !canvasObject.activeInHierarchy) return;
        if (Time.frameCount <= openedFrame) return;

        // Open時に NewRunButton を選択状態にする（Enterで押せる）
        if (pendingSelect)
        {
            pendingSelect = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(newRunButton.gameObject);
        }

        // Spaceでも NewRunButton を押す
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            newRunButton.onClick.Invoke();
    }

    private void BuildUI()
    {
        canvasObject = new GameObject("RunEndCanvas", typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var root = canvasObject.GetComponent<RectTransform>();

        var background = CreateRect("RunEndBackground", root);
        Stretch(background, Vector2.zero, Vector2.one);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.04f, 0.04f, 0.07f, 1f);
        backgroundImage.raycastTarget = true;

        titleText = CreateText("RunEndTitleText", root, "", 96f, TextAlignmentOptions.Center, Color.white);
        titleText.fontStyle = FontStyles.Bold;
        Stretch(titleText.rectTransform, new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.92f));

        summaryText = CreateText("RunEndSummaryText", root, "", 44f, TextAlignmentOptions.Center, Color.white);
        summaryText.lineSpacing = 20f;
        Stretch(summaryText.rectTransform, new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.70f));

        var buttonRect = CreateRect("NewRunButton", root);
        Stretch(buttonRect, new Vector2(0.37f, 0.10f), new Vector2(0.63f, 0.20f));
        var buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.40f, 0.25f, 1f);
        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        buttonRect.gameObject.AddComponent<UIFocusOutline>(); // キーボードで選択中の縁取り
        newRunButton = button;

        var buttonLabel = CreateText("Label", buttonRect, "新しいランを始める", 36f, TextAlignmentOptions.Center, Color.white);
        Stretch(buttonLabel.rectTransform, Vector2.zero, Vector2.one);

        var hint = CreateText("RunEndKeyHintText", root, "[Enter] または [Space] 新しいランを始める", 28f, TextAlignmentOptions.Center, new Color(0.75f, 0.78f, 0.88f));
        Stretch(hint.rectTransform, new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.08f));

        button.onClick.AddListener(() =>
        {
            RunState.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });

        canvasObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
        TextAlignmentOptions alignment, Color color)
    {
        var rt = CreateRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.richText = true;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }
}
