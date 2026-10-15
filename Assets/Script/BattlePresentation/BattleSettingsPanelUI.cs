using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// キー設定画面（右上の「設定」ボタン、または「設定を開く」のキーで開く）。UIはすべて実行時にコードで生成する。
// 注記：既存のLegacyにSettingsPanelUIという別のクラスがあるため、名前の衝突を避けて
// BattleSettingsPanelUIという名前にしている。
public class BattleSettingsPanelUI : MonoBehaviour
{
    // 設定画面を開いている間はtrue。盤面の操作・ショートカットキーは、この間は何もしない。
    // 閉じる操作と同じフレームの入力が盤面に漏れないよう、falseに戻るのはフレームの終わり（LateUpdate）。
    public static bool IsOpen { get; private set; }

    private const string CapturePrompt = "キーを押してください…";
    private const string DefaultMessage = "変更したいスロットをクリックして、割り当てたいキーを押す（Esc：キャンセル／Delete：未設定）";

    private static readonly GameAction[] Actions = (GameAction[])System.Enum.GetValues(typeof(GameAction));

    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;

    private GameObject settingsCanvas;
    private TextMeshProUGUI messageText;
    private readonly TextMeshProUGUI[,] slotLabels = new TextMeshProUGUI[10, KeyBindings.SlotCount];
    private readonly Button[,] slotButtons = new Button[10, KeyBindings.SlotCount];

    // キーの受付状態
    private bool capturing;
    private GameAction captureAction;
    private int captureSlot;
    private int captureStartFrame;

    // 受付中は、矢印キー・Enterで画面のボタンが動かないよう、UIのナビゲーションを止める
    private EventSystem navigationSystem;
    private bool restoreNavigation;
    private int restoreNavigationFrame;

    private bool closeRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
    }

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;

        BuildSettingsButton();
        BuildSettingsCanvas();

        KeyBindings.Changed += RefreshSlots;
    }

    void OnDestroy()
    {
        KeyBindings.Changed -= RefreshSlots;
        IsOpen = false;
        RestoreNavigation();
    }

    public void Open()
    {
        if (settingsCanvas == null) return;

        closeRequested = false;
        IsOpen = true;
        settingsCanvas.SetActive(true);

        messageText.text = DefaultMessage;
        RefreshSlots();

        // 開いたときに、最初のスロットのボタンを選択状態にする
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(slotButtons[0, 0].gameObject);
    }

    public void Close()
    {
        if (!IsOpen) return;

        if (capturing) EndCapture();

        settingsCanvas.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        closeRequested = true; // IsOpenはフレームの終わりにfalseへ戻す
    }

    public void Toggle()
    {
        if (IsOpen && !closeRequested) Close();
        else Open();
    }

    void Update()
    {
        if (restoreNavigation && Time.frameCount > restoreNavigationFrame) RestoreNavigation();

        if (Keyboard.current == null) return;

        // キーの受付中：次に押されたキーを処理する
        if (capturing)
        {
            if (Time.frameCount > captureStartFrame) HandleCapture(Keyboard.current);
            return;
        }

        if (IsOpen && !closeRequested)
        {
            if (KeyBindings.WasPressed(GameAction.OpenSettings) || KeyBindings.WasPressed(GameAction.Cancel))
                Close();
        }
        else if (!IsOpen && KeyBindings.WasPressed(GameAction.OpenSettings))
        {
            Open();
        }
    }

    void LateUpdate()
    {
        if (closeRequested)
        {
            closeRequested = false;
            IsOpen = false;
        }
    }

    // ---------------------------------------------------------------
    // キーの受付
    // ---------------------------------------------------------------

    private void BeginCapture(GameAction action, int slot)
    {
        capturing = true;
        captureAction = action;
        captureSlot = slot;
        captureStartFrame = Time.frameCount; // クリック（または決定キー）と同じフレームのキーは受け付けない

        // 受付中は、UIのナビゲーション（矢印キー・Enter）を止める
        if (EventSystem.current != null)
        {
            navigationSystem = EventSystem.current;
            navigationSystem.sendNavigationEvents = false;
        }
        restoreNavigation = false;

        RefreshSlots();
    }

    private void EndCapture()
    {
        capturing = false;

        // 受付を終えたキーがUIのSubmitとして処理されないよう、ナビゲーションの復帰は次のフレームにする
        restoreNavigation = true;
        restoreNavigationFrame = Time.frameCount;

        RefreshSlots();
    }

    private void RestoreNavigation()
    {
        restoreNavigation = false;
        if (navigationSystem != null) navigationSystem.sendNavigationEvents = true;
        navigationSystem = null;
    }

    private void HandleCapture(Keyboard keyboard)
    {
        // Escape：受付をキャンセルする
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            EndCapture();
            return;
        }

        // Delete：そのスロットを未設定にする
        if (keyboard.deleteKey.wasPressedThisFrame)
        {
            var action = captureAction;
            int slot = captureSlot;
            EndCapture();
            KeyBindings.Clear(action, slot);
            messageText.text = DefaultMessage;
            return;
        }

        foreach (var key in keyboard.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            if (IsIgnoredKey(key.keyCode)) continue;

            var entry = new KeyBindingEntry(key.keyCode, keyboard.shiftKey.isPressed);
            var action = captureAction;
            int slot = captureSlot;

            EndCapture();
            var cleared = KeyBindings.Set(action, slot, entry);

            messageText.text = cleared.HasValue
                ? $"「{GameActionInfo.GetDisplayName(cleared.Value)}」の割り当てを解除しました（同じキーを使っていたため）"
                : DefaultMessage;
            return;
        }
    }

    // Shift・Ctrl・Altのキー自体は割り当てない
    private static bool IsIgnoredKey(Key key)
    {
        switch (key)
        {
            case Key.None:
            case Key.IMESelected:
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftAlt:
            case Key.RightAlt:
                return true;
            default:
                return false;
        }
    }

    // ---------------------------------------------------------------
    // 表示
    // ---------------------------------------------------------------

    private void RefreshSlots()
    {
        if (slotLabels[0, 0] == null) return;

        for (int a = 0; a < Actions.Length; a++)
        {
            for (int slot = 0; slot < KeyBindings.SlotCount; slot++)
            {
                bool isCapturingThis = capturing && captureAction == Actions[a] && captureSlot == slot;
                slotLabels[a, slot].text = isCapturingThis
                    ? CapturePrompt
                    : KeyBindings.GetEntryLabel(KeyBindings.GetBinding(Actions[a], slot));
            }
        }
    }

    // ---------------------------------------------------------------
    // UI生成
    // ---------------------------------------------------------------

    private Canvas CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // 画面右上（右端・上端から24px内側）の「設定」ボタン
    private void BuildSettingsButton()
    {
        var canvas = CreateCanvas("SettingsButtonCanvas", 96);

        var button = CreateButton("SettingsButton", canvas.transform, "設定", new Color(0.22f, 0.24f, 0.32f, 1f), 26f, out _);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(140f, 56f);

        button.onClick.AddListener(Toggle);
    }

    private void BuildSettingsCanvas()
    {
        var canvas = CreateCanvas("SettingsCanvas", 97);
        settingsCanvas = canvas.gameObject;
        var root = canvas.GetComponent<RectTransform>();

        var background = CreateRect("SettingsBackground", root);
        Stretch(background, Vector2.zero, Vector2.one);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.78f);
        backgroundImage.raycastTarget = true;

        var title = CreateText("SettingsTitleText", root, "キー設定", 56f, TextAlignmentOptions.Center, Color.white);
        Stretch(title.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 0.98f));

        // 操作ごとの行（GameActionの順）
        var rows = CreateRect("SettingsRows", root);
        Stretch(rows, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.86f));

        int count = Actions.Length;
        for (int a = 0; a < count; a++)
        {
            var action = Actions[a];

            var row = CreateRect($"SettingsRow_{action}", rows);
            Stretch(row, new Vector2(0f, 1f - (a + 1f) / count), new Vector2(1f, 1f - a / (float)count));
            row.offsetMin = new Vector2(0f, 4f);
            row.offsetMax = new Vector2(0f, -4f);

            var name = CreateText("ActionName", row, GameActionInfo.GetDisplayName(action), 28f, TextAlignmentOptions.MidlineLeft, Color.white);
            Stretch(name.rectTransform, new Vector2(0f, 0f), new Vector2(0.40f, 1f));

            for (int slot = 0; slot < KeyBindings.SlotCount; slot++)
            {
                float x0 = slot == 0 ? 0.42f : 0.72f;
                float x1 = slot == 0 ? 0.70f : 1.0f;

                var button = CreateButton($"Slot_{action}_{slot}", row, "", new Color(0.20f, 0.22f, 0.30f, 1f), 26f, out var label);
                Stretch(button.GetComponent<RectTransform>(), new Vector2(x0, 0f), new Vector2(x1, 1f));
                label.alignment = TextAlignmentOptions.Center;

                var capturedAction = action;
                int capturedSlot = slot;
                button.onClick.AddListener(() => BeginCapture(capturedAction, capturedSlot));

                slotButtons[a, slot] = button;
                slotLabels[a, slot] = label;
            }
        }

        messageText = CreateText("SettingsMessageText", root, DefaultMessage, 24f, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.6f));
        Stretch(messageText.rectTransform, new Vector2(0.2f, 0.115f), new Vector2(0.8f, 0.165f));

        var resetButton = CreateButton("ResetButton", root, "初期設定に戻す", new Color(0.42f, 0.26f, 0.26f, 1f), 30f, out _);
        Stretch(resetButton.GetComponent<RectTransform>(), new Vector2(0.2f, 0.03f), new Vector2(0.48f, 0.10f));
        resetButton.onClick.AddListener(() =>
        {
            KeyBindings.ResetToDefault();
            messageText.text = "初期設定に戻しました";
        });

        var closeButton = CreateButton("CloseButton", root, "閉じる", new Color(0.15f, 0.40f, 0.25f, 1f), 30f, out _);
        Stretch(closeButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.03f), new Vector2(0.8f, 0.10f));
        closeButton.onClick.AddListener(Close);

        settingsCanvas.SetActive(false);
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

    private Button CreateButton(string name, Transform parent, string text, Color color, float fontSize, out TextMeshProUGUI label)
    {
        var rt = CreateRect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<UIFocusOutline>(); // キーボードで選択中の縁取り

        label = CreateText("Label", rt, text, fontSize, TextAlignmentOptions.Center, Color.white);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);

        return button;
    }
}
