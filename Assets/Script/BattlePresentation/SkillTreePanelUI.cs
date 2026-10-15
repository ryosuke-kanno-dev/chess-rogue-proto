using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Chebyss.Battle;

// キングのスキルツリー画面。装備画面の「スキル」ボタンから開く。UIはすべて実行時にコードで生成する。
// スキルの取得は「取得する」ボタンを押したときだけ（誤操作の防止）。
public class SkillTreePanelUI : MonoBehaviour
{
    // 開いている間はtrue。閉じる操作と同じフレームの入力が漏れないよう、falseに戻るのはフレームの終わり（LateUpdate）。
    public static bool IsOpen { get; private set; }

    private static readonly Color OwnedColor = new Color(0.78f, 0.62f, 0.12f, 1f);       // 取得済み（金色）
    private static readonly Color AcquirableColor = new Color(0.30f, 0.42f, 0.66f, 1f);  // 取得できる
    private static readonly Color LockedColor = new Color(0.18f, 0.18f, 0.21f, 1f);      // 取得できない（暗いグレー）
    private static readonly Color SuccessMessageColor = new Color(0.5f, 0.9f, 0.6f);
    private static readonly Color ErrorMessageColor = new Color(1f, 0.5f, 0.5f);
    private static readonly Color PanelButtonColor = new Color(0.22f, 0.24f, 0.32f, 1f);

    private static readonly SkillBranch[] BranchOrder =
    {
        SkillBranch.Info, SkillBranch.Combat, SkillBranch.Survival, SkillBranch.Gathering
    };

    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;

    private GameObject canvasObject;
    private TextMeshProUGUI skillPointText;
    private TextMeshProUGUI descriptionText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI skillResultText;
    private Button acquireButton;
    private Button closeButton;

    private readonly Dictionary<string, Button> nodeButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Image> nodeImages = new Dictionary<string, Image>();
    private readonly Dictionary<string, TextMeshProUGUI> nodeLabels = new Dictionary<string, TextMeshProUGUI>();

    private string selectedId;
    private string hoveredId;

    private bool closeRequested;
    private int openedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
    }

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;

        BuildUI();
        RefreshHint();

        KeyBindings.Changed += RefreshHint;
    }

    void OnDestroy()
    {
        KeyBindings.Changed -= RefreshHint;
        IsOpen = false;
    }

    public void Open()
    {
        if (canvasObject == null) return;

        closeRequested = false;
        IsOpen = true;
        openedFrame = Time.frameCount;
        selectedId = null;
        hoveredId = null;

        canvasObject.SetActive(true);
        SetResult("", Color.white);
        Refresh();
        RefreshHint();

        // 最初に取得できるスキル（なければ「閉じる」）を選択状態にする
        GameObject target = closeButton.gameObject;
        foreach (var skill in KingSkillCatalog.All)
        {
            if (RunState.CanAcquireSkill(skill.id))
            {
                target = nodeButtons[skill.id].gameObject;
                break;
            }
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    public void Close()
    {
        if (!IsOpen) return;

        canvasObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        closeRequested = true; // IsOpenはフレームの終わりにfalseへ戻す
    }

    void Update()
    {
        if (!IsOpen || closeRequested) return;

        // キー設定画面を開いている間は、何もしない
        if (BattleSettingsPanelUI.IsOpen) return;

        // 開いたフレームの入力は無視する
        if (Time.frameCount <= openedFrame) return;

        // キー設定の「キャンセル」で閉じる
        if (KeyBindings.WasPressed(GameAction.Cancel))
        {
            Close();
            return;
        }

        // 「ターン終了・配置完了・次へ・スキル取得」のキーで、「取得する」ボタンと同じ処理を行う
        if (KeyBindings.WasPressed(GameAction.EndTurn)) TryAcquireSelected();
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
    // 操作
    // ---------------------------------------------------------------

    private void SelectSkill(string id)
    {
        // ノードの選択が変わったときは、結果メッセージを消す
        if (selectedId != id) SetResult("", Color.white);

        selectedId = id;
        Refresh();
    }

    private void OnAcquireClicked()
    {
        TryAcquireSelected();
    }

    // 「取得する」ボタン（マウス）と、キー（EndTurn）で共通の処理。結果は必ず結果メッセージに表示する。
    private void TryAcquireSelected()
    {
        if (selectedId == null || !KingSkillCatalog.TryGet(selectedId, out var skill))
        {
            SetResult("取得するスキルを選んでください", ErrorMessageColor);
            return;
        }

        if (RunState.HasSkill(skill.id))
        {
            SetResult("取得できません：取得済みです", ErrorMessageColor);
            return;
        }

        if (!ArePrerequisitesOwned(skill))
        {
            SetResult("取得できません：前提のスキルが必要です", ErrorMessageColor);
            return;
        }

        if (!RunState.TryAcquireSkill(skill.id))
        {
            SetResult("取得できません：スキルポイントが足りません", ErrorMessageColor);
            return;
        }

        Refresh();

        // 「取得する」が無効になっても、キーボードの選択が途切れないよう、取得したノードへ戻す
        if (EventSystem.current != null && nodeButtons.TryGetValue(skill.id, out var node))
            EventSystem.current.SetSelectedGameObject(node.gameObject);

        // 選択の移動で結果メッセージが消えないよう、メッセージは最後に表示する
        SetResult($"「{skill.displayName}」を取得しました", SuccessMessageColor);
    }

    private void SetResult(string message, Color color)
    {
        if (skillResultText == null) return;
        skillResultText.text = message;
        skillResultText.color = color;
    }

    // ---------------------------------------------------------------
    // 表示
    // ---------------------------------------------------------------

    private void Refresh()
    {
        skillPointText.text = $"スキルポイント: {RunState.SkillPoints}";

        foreach (var skill in KingSkillCatalog.All)
        {
            bool owned = RunState.HasSkill(skill.id);
            bool canAcquire = RunState.CanAcquireSkill(skill.id);

            nodeImages[skill.id].color = owned ? OwnedColor : canAcquire ? AcquirableColor : LockedColor;
            nodeLabels[skill.id].text = $"{(owned ? "✔ " : "")}{skill.displayName}　{skill.cost}SP";
        }

        acquireButton.interactable = selectedId != null && RunState.CanAcquireSkill(selectedId);
        RefreshDescription();
    }

    // ホバー中（なければ選択中）のスキルの説明
    private void RefreshDescription()
    {
        string id = hoveredId ?? selectedId;
        if (id == null || !KingSkillCatalog.TryGet(id, out var skill))
        {
            descriptionText.text = "スキルを選ぶと、ここに説明が表示されます。";
            return;
        }

        var prerequisites = new List<string>();
        foreach (var prerequisiteId in skill.prerequisites)
        {
            prerequisites.Add(KingSkillCatalog.TryGet(prerequisiteId, out var prerequisite)
                ? prerequisite.displayName
                : prerequisiteId);
        }

        string state;
        if (RunState.HasSkill(skill.id)) state = "<color=#FFD060>取得済み</color>";
        else if (RunState.CanAcquireSkill(skill.id)) state = "<color=#80E0A0>取得できる</color>";
        else if (!ArePrerequisitesOwned(skill)) state = "<color=#FF8080>前提のスキルが必要です</color>";
        else state = "<color=#FF8080>スキルポイントが足りません</color>";

        var sb = new StringBuilder();
        sb.Append($"<b>{skill.displayName}</b>　{skill.cost}SP\n");
        sb.Append(skill.description).Append('\n');
        sb.Append("前提：").Append(prerequisites.Count > 0 ? string.Join("、", prerequisites) : "なし").Append('\n');
        sb.Append(state);
        descriptionText.text = sb.ToString();
    }

    private static bool ArePrerequisitesOwned(KingSkillDefinition skill)
    {
        foreach (var prerequisite in skill.prerequisites)
        {
            if (!RunState.HasSkill(prerequisite)) return false;
        }
        return true;
    }

    private void RefreshHint()
    {
        if (hintText == null) return;
        hintText.text = $"[矢印] ノードを選ぶ　[{KeyBindings.GetLabel(GameAction.EndTurn)}] 取得する　[{KeyBindings.GetLabel(GameAction.Cancel)}] 閉じる";
    }

    // ---------------------------------------------------------------
    // UI生成
    // ---------------------------------------------------------------

    private void BuildUI()
    {
        canvasObject = new GameObject("SkillTreeCanvas", typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 91;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var root = canvasObject.GetComponent<RectTransform>();

        var background = CreateRect("SkillTreeBackground", root);
        Stretch(background, Vector2.zero, Vector2.one);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.05f, 0.05f, 0.09f, 1f);
        backgroundImage.raycastTarget = true;

        var title = CreateText("SkillTreeTitleText", root, "キングのスキル", 54f, TextAlignmentOptions.Center, Color.white);
        Stretch(title.rectTransform, new Vector2(0f, 0.90f), new Vector2(1f, 0.98f));

        skillPointText = CreateText("SkillPointText", root, "", 34f, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.9f, 0.4f));
        Stretch(skillPointText.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.5f, 0.90f));

        BuildColumns(root);

        descriptionText = CreateText("SkillDescriptionText", root, "", 26f, TextAlignmentOptions.TopLeft, Color.white);
        Stretch(descriptionText.rectTransform, new Vector2(0.05f, 0.12f), new Vector2(0.68f, 0.32f));

        // 取得の結果（成功・失敗の理由）を出す1行。説明欄の下、操作ヒントの上。
        skillResultText = CreateText("SkillResultText", root, "", 26f, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(skillResultText.rectTransform, new Vector2(0.05f, 0.075f), new Vector2(0.68f, 0.12f));

        acquireButton = CreateButton("AcquireButton", root, "取得する", new Color(0.15f, 0.42f, 0.27f, 1f), 30f);
        Stretch(acquireButton.GetComponent<RectTransform>(), new Vector2(0.72f, 0.21f), new Vector2(0.95f, 0.30f));
        acquireButton.onClick.AddListener(OnAcquireClicked);

        closeButton = CreateButton("CloseButton", root, "閉じる", PanelButtonColor, 30f);
        Stretch(closeButton.GetComponent<RectTransform>(), new Vector2(0.72f, 0.10f), new Vector2(0.95f, 0.19f));
        closeButton.onClick.AddListener(Close);

        hintText = CreateText("SkillTreeKeyHintText", root, "", 24f, TextAlignmentOptions.Center, new Color(0.75f, 0.78f, 0.88f));
        Stretch(hintText.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.07f));

        canvasObject.SetActive(false);
    }

    // 枝ごとの列（情報・戦闘・生存・収集）。各列に、その枝のスキルを段（tier）の順に上から並べる。
    private void BuildColumns(RectTransform root)
    {
        const int maxNodes = 5;
        const float headerFraction = 0.12f;

        for (int column = 0; column < BranchOrder.Length; column++)
        {
            var branch = BranchOrder[column];

            float x0 = 0.05f + column * 0.225f;
            var columnRect = CreateRect($"SkillColumn_{branch}", root);
            Stretch(columnRect, new Vector2(x0, 0.34f), new Vector2(x0 + 0.21f, 0.84f));

            var header = CreateText("ColumnHeader", columnRect, GetBranchName(branch), 34f, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 1f));
            header.fontStyle = FontStyles.Bold;
            Stretch(header.rectTransform, new Vector2(0f, 1f - headerFraction), new Vector2(1f, 1f));

            // 段（tier）の順、同じ段ではカタログの順
            var skills = new List<KingSkillDefinition>();
            for (int tier = 1; tier <= 3; tier++)
            {
                foreach (var skill in KingSkillCatalog.All)
                {
                    if (skill.branch == branch && skill.tier == tier) skills.Add(skill);
                }
            }

            float nodeAreaTop = 1f - headerFraction;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var id = skill.id;

                var button = CreateButton($"SkillNode_{id}", columnRect, "", LockedColor, 24f);
                var rect = button.GetComponent<RectTransform>();
                Stretch(rect, new Vector2(0f, nodeAreaTop * (1f - (i + 1f) / maxNodes)),
                              new Vector2(1f, nodeAreaTop * (1f - i / (float)maxNodes)));
                rect.offsetMin = new Vector2(0f, 6f);
                rect.offsetMax = new Vector2(0f, -6f);

                button.onClick.AddListener(() => SelectSkill(id));

                // ホバーで説明を表示し、キーボードで選択が移ったときは選択中のスキルにする
                AddTrigger(button.gameObject, EventTriggerType.PointerEnter, () => { hoveredId = id; RefreshDescription(); });
                AddTrigger(button.gameObject, EventTriggerType.PointerExit, () =>
                {
                    if (hoveredId == id) hoveredId = null;
                    RefreshDescription();
                });
                AddTrigger(button.gameObject, EventTriggerType.Select, () => SelectSkill(id));

                nodeButtons[id] = button;
                nodeImages[id] = button.GetComponent<Image>();
                nodeLabels[id] = button.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
    }

    private static string GetBranchName(SkillBranch branch)
    {
        switch (branch)
        {
            case SkillBranch.Info:      return "情報";
            case SkillBranch.Combat:    return "戦闘";
            case SkillBranch.Survival:  return "生存";
            case SkillBranch.Gathering: return "収集";
            default:                    return branch.ToString();
        }
    }

    private static void AddTrigger(GameObject target, EventTriggerType type, System.Action action)
    {
        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
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

    private Button CreateButton(string name, Transform parent, string text, Color color, float fontSize)
    {
        var rt = CreateRect(name, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rt.gameObject.AddComponent<UIFocusOutline>(); // キーボードで選択中の縁取り

        var label = CreateText("Label", rt, text, fontSize, TextAlignmentOptions.Center, Color.white);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);

        return button;
    }
}
