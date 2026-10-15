using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Chebyss.Battle;

// 戦闘中の階数表示（画面左上）。UIは実行時にコードで生成する。ボス階では階の種類も表示する。
public class RunHudUI : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;
    private TextMeshProUGUI floorText;
    private TextMeshProUGUI placementHintText;
    private bool lastPlacementPhase = true;
    private PieceType? lastSelectedType;
    private bool hintInitialized;
    private int lastShownFloor = -1;

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;
        BuildUI();
        Refresh();

        // キー設定が変わったら、配置フェーズの案内を作り直す
        KeyBindings.Changed += OnKeyBindingsChanged;
    }

    void Update()
    {
        // 階が進んだとき（PrepareNextFloor後など）だけ更新する
        if (floorText != null && RunState.CurrentFloor != lastShownFloor) Refresh();

        // 配置フェーズ中だけ案内を出す（バトル開始で非表示にする）。選択状況に応じて文言を切り替える。
        if (placementHintText != null)
        {
            bool phase = bootstrapper.IsPlacementPhase;
            if (phase != lastPlacementPhase)
            {
                lastPlacementPhase = phase;
                placementHintText.gameObject.SetActive(phase);
            }

            if (phase)
            {
                var selected = GetSelectedType();
                if (!hintInitialized || selected != lastSelectedType)
                {
                    hintInitialized = true;
                    lastSelectedType = selected;
                    placementHintText.text = BuildHintText(selected);
                }
            }
        }
    }

    private void OnKeyBindingsChanged()
    {
        hintInitialized = false;
    }

    void OnDestroy()
    {
        KeyBindings.Changed -= OnKeyBindingsChanged;
    }

    private PieceType? GetSelectedType()
    {
        var input = bootstrapper.PlacementInput;
        return input != null ? input.SelectedType : null;
    }

    private static string BuildHintText(PieceType? selected)
    {
        if (!selected.HasValue)
            return $"配置フェーズ：駒をドラッグ、または駒をクリックしてからマスをクリックして配置　／　準備ができたら「配置完了」を押す　／ キーボード：{KeyBindings.GetLabel(GameAction.NextPiece)} で駒を選ぶ";

        return $"{GetPieceName(selected.Value)}を選択中：置きたいマスをクリック、または {KeyBindings.GetMoveLabel()} でカーソルを動かして {KeyBindings.GetLabel(GameAction.Confirm)}（右クリック／{KeyBindings.GetLabel(GameAction.Cancel)} で選択解除）";
    }

    private static string GetPieceName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Knight: return "ナイト";
            case PieceType.Rook:   return "ルーク";
            case PieceType.Bishop: return "ビショップ";
            case PieceType.Pawn:   return "ポーン";
            default:               return type.ToString();
        }
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("RunHudCanvas", typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var textObject = new GameObject("FloorText", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);

        var rt = textObject.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -20f);
        rt.sizeDelta = new Vector2(420f, 110f);

        floorText = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) floorText.font = font;
        floorText.richText = true;
        floorText.fontSize = 44f;
        floorText.fontStyle = FontStyles.Bold;
        floorText.alignment = TextAlignmentOptions.TopLeft;
        floorText.color = Color.white;
        floorText.raycastTarget = false;

        // FloorTextの下に置く配置フェーズの案内（小さめの文字）
        var hintObject = new GameObject("PlacementHintText", typeof(RectTransform));
        hintObject.transform.SetParent(canvasObject.transform, false);

        var hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 1f);
        hintRect.anchorMax = new Vector2(0f, 1f);
        hintRect.pivot = new Vector2(0f, 1f);
        hintRect.anchoredPosition = new Vector2(24f, -135f);
        hintRect.sizeDelta = new Vector2(1500f, 40f);

        placementHintText = hintObject.AddComponent<TextMeshProUGUI>();
        if (font != null) placementHintText.font = font;
        placementHintText.richText = true;
        placementHintText.fontSize = 24f;
        placementHintText.alignment = TextAlignmentOptions.TopLeft;
        placementHintText.color = new Color(0.85f, 0.9f, 1f);
        placementHintText.raycastTarget = false;
        placementHintText.text = BuildHintText(GetSelectedType());
        hintInitialized = true;
        lastSelectedType = GetSelectedType();

        lastPlacementPhase = bootstrapper.IsPlacementPhase;
        hintObject.SetActive(lastPlacementPhase);
    }

    private void Refresh()
    {
        int floor = RunState.CurrentFloor;
        lastShownFloor = floor;

        string text = $"{floor}F / {bootstrapper.TotalFloors}F";

        string rank = EnemyNames.GetBossRankName(floor, bootstrapper.BossFloorInterval, bootstrapper.FinalFloor);
        if (!string.IsNullOrEmpty(rank))
            text += $"\n<size=70%><color=#FF6060>{rank}階</color></size>";

        floorText.text = text;
    }
}
