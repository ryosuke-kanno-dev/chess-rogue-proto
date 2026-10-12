using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Chebyss.Battle;

// マウスだけで遊べるようにするための画面下部のボタン（待機・キャンセル）。
// BattlePhaseActionButton（配置完了／ターン終了）がシーンに無い場合のみ、右下に同等のボタンも作る。
// UIはすべて実行時にコードで生成する。
public class BattleControlsUI : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;
    private BattleInputController battleInput;

    private GameObject controlsRow;
    private Button waitButton;
    private Button cancelButton;
    private TextMeshProUGUI cancelLabel;

    private Button phaseButton;
    private TextMeshProUGUI phaseLabel;
    private TextMeshProUGUI keyHintText;

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;
        BuildUI();
    }

    void Start()
    {
        if (EventSystem.current == null)
            Debug.LogWarning("[BattleControlsUI] シーンに EventSystem がありません。画面上のボタンが反応しません。");
    }

    void Update()
    {
        if (bootstrapper == null) return;
        if (battleInput == null) battleInput = FindFirstObjectByType<BattleInputController>();

        var turnController = bootstrapper.TurnController;
        bool isPlayerTurn = turnController != null && turnController.CurrentPhase == BattlePhase.PlayerTurn;

        if (bootstrapper.IsPlacementPhase)
        {
            // 配置フェーズ：待機は出さない。駒を選択中のときだけ「選択解除」を出す
            var selected = bootstrapper.PlacementInput != null ? bootstrapper.PlacementInput.SelectedType : null;
            bool hasSelection = selected.HasValue;

            controlsRow.SetActive(hasSelection);
            waitButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(hasSelection);
            cancelButton.interactable = true;
            SetLabel(cancelLabel, "選択解除");
        }
        else if (isPlayerTurn)
        {
            bool hasSelection = battleInput != null && battleInput.HasSelection;
            bool isPursuing = battleInput != null && battleInput.IsPursuing;

            controlsRow.SetActive(true);
            waitButton.gameObject.SetActive(true);
            waitButton.interactable = hasSelection;
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = hasSelection || isPursuing;
            SetLabel(cancelLabel, isPursuing ? "追撃しない" : "キャンセル");
        }
        else
        {
            // 敵ターン・勝敗確定後はパネル全体を隠す
            controlsRow.SetActive(false);
        }

        UpdatePhaseButton(isPlayerTurn);
        UpdateKeyHint(isPlayerTurn);
    }

    // キー操作のヒント（状況に応じて文言を切り替える。敵ターン・勝敗確定後は非表示）
    private void UpdateKeyHint(bool isPlayerTurn)
    {
        string hint = null;

        if (bootstrapper.IsPlacementPhase)
        {
            bool hasSelection = bootstrapper.PlacementInput != null && bootstrapper.PlacementInput.SelectedType.HasValue;
            hint = hasSelection
                ? "[Enter] ここに置く　[矢印/WASD] カーソル　[Tab] 次の駒　[Esc] 選択解除　[Space] 配置完了"
                : "[Tab] 駒を選ぶ　[矢印/WASD] カーソル　[Enter] 駒を選ぶ　[Space] 配置完了";
        }
        else if (isPlayerTurn)
        {
            bool hasSelection = battleInput != null && battleInput.HasSelection;
            bool isPursuing = battleInput != null && battleInput.IsPursuing;

            if (isPursuing)
                hint = "[Tab] 追撃先　[Enter] 追撃　[Esc] 追撃しない　[Space] ターン終了";
            else if (hasSelection)
                hint = "[矢印/WASD] カーソル　[Tab] 移動・攻撃先　[Enter] 決定　[Esc] キャンセル　[Space] ターン終了";
            else
                hint = "[矢印/WASD] カーソル　[Tab] 次の駒　[Enter] 駒を選ぶ　[Space] ターン終了";
        }

        keyHintText.gameObject.SetActive(hint != null);
        if (hint != null) SetLabel(keyHintText, hint);
    }

    private void UpdatePhaseButton(bool isPlayerTurn)
    {
        if (phaseButton == null) return;

        if (bootstrapper.IsPlacementPhase)
        {
            phaseButton.gameObject.SetActive(true);
            phaseButton.interactable = bootstrapper.Placement != null && bootstrapper.Placement.IsComplete;
            SetLabel(phaseLabel, "配置完了");
        }
        else if (isPlayerTurn)
        {
            phaseButton.gameObject.SetActive(true);
            phaseButton.interactable = true;
            SetLabel(phaseLabel, "ターン終了");
        }
        else
        {
            phaseButton.gameObject.SetActive(false);
        }
    }

    private static void SetLabel(TextMeshProUGUI label, string text)
    {
        if (label != null && label.text != text) label.text = text;
    }

    // ---------------------------------------------------------------
    // UI生成
    // ---------------------------------------------------------------

    private void BuildUI()
    {
        var canvasObject = new GameObject("BattleControlsCanvas", typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var root = canvasObject.GetComponent<RectTransform>();

        // 画面下部の中央（下端から24px上）に、横並びのボタン
        var row = CreateRect("ControlsRow", root);
        row.anchorMin = new Vector2(0.5f, 0f);
        row.anchorMax = new Vector2(0.5f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = new Vector2(0f, 24f);

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        controlsRow = row.gameObject;

        waitButton = CreateButton("WaitButton", row, "待機", new Color(0.20f, 0.32f, 0.52f, 1f), 220f, 64f, 30f, out _);
        waitButton.onClick.AddListener(() =>
        {
            if (battleInput != null) battleInput.RequestWait();
        });

        cancelButton = CreateButton("CancelButton", row, "キャンセル", new Color(0.42f, 0.26f, 0.26f, 1f), 220f, 64f, 30f, out cancelLabel);
        cancelButton.onClick.AddListener(OnCancelClicked);

        // 既にBattlePhaseActionButtonがある場合は、重複を避けて何も作らない
        if (FindFirstObjectByType<BattlePhaseActionButton>() == null)
        {
            phaseButton = CreateButton("PhaseButton", root, "配置完了", new Color(0.15f, 0.42f, 0.27f, 1f), 300f, 80f, 34f, out phaseLabel);

            var rect = phaseButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);

            phaseButton.onClick.AddListener(() => bootstrapper.AdvancePhase());
        }

        // 既存のボタン列の上（下端から100px上）に、操作ヒントを1行表示する
        var hintRect = CreateRect("KeyHintText", root);
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 100f);
        hintRect.sizeDelta = new Vector2(1800f, 36f);

        keyHintText = hintRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) keyHintText.font = font;
        keyHintText.richText = true;
        keyHintText.fontSize = 24f;
        keyHintText.alignment = TextAlignmentOptions.Center;
        keyHintText.color = new Color(1f, 1f, 1f, 0.75f);
        keyHintText.raycastTarget = false;
        hintRect.gameObject.SetActive(false);

        controlsRow.SetActive(false);
    }

    private void OnCancelClicked()
    {
        if (bootstrapper.IsPlacementPhase)
        {
            if (bootstrapper.PlacementInput != null) bootstrapper.PlacementInput.Deselect();
        }
        else if (battleInput != null)
        {
            battleInput.RequestCancel();
        }
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private Button CreateButton(string name, Transform parent, string text, Color color,
        float width, float height, float fontSize, out TextMeshProUGUI label)
    {
        var rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(width, height);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // 無効時は暗いグレーにする
        var colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        button.colors = colors;

        var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;

        var labelRect = CreateRect("Label", rect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.richText = true;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;

        return button;
    }
}
