using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Chebyss.Battle;

// 勝利後の全画面の装備画面（1駒3スロット）。UIはすべて実行時にコードで生成する（シーン編集不要）。
// 状態（所持品・装備）はRunStateが唯一の真実で、このクラスは表示と操作の橋渡しのみを行う。
public class LoadoutPanelUI : MonoBehaviour
{
    private static readonly PieceType[] PieceOrder =
    {
        PieceType.Knight, PieceType.Rook, PieceType.Bishop, PieceType.Pawn
    };

    private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.14f, 1f);
    private static readonly Color ButtonColor = new Color(0.20f, 0.20f, 0.28f, 1f);
    private static readonly Color EmptySlotColor = new Color(0.14f, 0.14f, 0.19f, 1f);
    private static readonly Color SelectedColor = new Color(0.55f, 0.45f, 0.12f, 1f);
    private static readonly Color PrimaryButtonColor = new Color(0.15f, 0.40f, 0.25f, 1f);

    private class SlotView
    {
        public Image image;
        public TextMeshProUGUI label;
    }

    private BattleBootstrapper bootstrapper;
    private TMP_FontAsset font;

    private GameObject canvasObject;
    private RectTransform inventoryContent;
    private TextMeshProUGUI nextFloorInfoText;

    // 次の階の盤面の簡易図
    private Image[,] mapCells;
    private TextMeshProUGUI[,] mapLabels;
    private GameObject mapLockedOverlay;

    private readonly Dictionary<PieceType, TextMeshProUGUI> statTexts = new();
    private readonly Dictionary<PieceType, SlotView[]> slotViews = new();
    private readonly List<GameObject> inventoryItemObjects = new();

    private EquipmentInstance selectedItem;
    private readonly HashSet<string> newItemIds = new();

    public void Initialize(BattleBootstrapper bootstrapper, TMP_FontAsset font)
    {
        this.bootstrapper = bootstrapper;
        this.font = font;
        BuildUI();
    }

    // 今回の戦利品を「NEW」として先頭に並べ、画面を開く
    public void Open(IReadOnlyList<EquipmentInstance> newItems)
    {
        newItemIds.Clear();
        if (newItems != null)
        {
            foreach (var item in newItems)
                if (item != null) newItemIds.Add(item.itemId);
        }

        selectedItem = null;

        if (EventSystem.current == null)
            Debug.LogWarning("[LoadoutPanelUI] シーンに EventSystem がありません。装備画面のボタンが反応しません。");

        Refresh();
        canvasObject.SetActive(true);
    }

    // ---------------------------------------------------------------
    // UI生成
    // ---------------------------------------------------------------

    private void BuildUI()
    {
        canvasObject = new GameObject("LoadoutCanvas", typeof(RectTransform));

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var root = canvasObject.GetComponent<RectTransform>();

        var background = CreateRect("LoadoutBackground", root);
        Stretch(background, Vector2.zero, Vector2.one);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.05f, 0.05f, 0.08f, 1f); // 背後の「Turn」表示が透けないよう不透明
        backgroundImage.raycastTarget = true;

        var title = CreateText("TitleText", root, "勝利！ 装備を整える", 56f, TextAlignmentOptions.Center, Color.white, false);
        Stretch(title.rectTransform, new Vector2(0f, 0.91f), new Vector2(1f, 1f));

        BuildPieceColumn(root);
        BuildInventoryPanel(root);
        BuildNextFloorPanel(root);
        BuildNextBattleButton(root);

        canvasObject.SetActive(false);
    }

    private void BuildPieceColumn(RectTransform root)
    {
        var column = CreateRect("PieceColumn", root);
        Stretch(column, new Vector2(0.02f, 0.10f), new Vector2(0.50f, 0.90f));

        for (int i = 0; i < PieceOrder.Length; i++)
        {
            var type = PieceOrder[i];

            var row = CreateRect($"PieceRow_{type}", column);
            Stretch(row, new Vector2(0f, 1f - (i + 1) / 4f), new Vector2(1f, 1f - i / 4f));
            row.offsetMin = new Vector2(0f, 6f);
            row.offsetMax = new Vector2(0f, -6f);
            row.gameObject.AddComponent<Image>().color = PanelColor;

            // 「ビショップ」が1行に収まるよう、自動サイズ（最小18）＋<nobr>で折り返しを禁止する
            var label = CreateText($"PieceLabel_{type}", row, $"<nobr>{GetPieceDisplayName(type)}</nobr>", 30f, TextAlignmentOptions.Center, Color.white, true);
            label.fontSizeMin = 18f;
            Stretch(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.15f, 1f));

            var stat = CreateText($"StatText_{type}", row, "", 21f, TextAlignmentOptions.MidlineLeft, Color.white, true);
            Stretch(stat.rectTransform, new Vector2(0.15f, 0.68f), new Vector2(0.99f, 1f));
            statTexts[type] = stat;

            var views = new SlotView[RunState.SlotsPerPiece];
            for (int s = 0; s < RunState.SlotsPerPiece; s++)
            {
                float x0 = 0.15f + s * 0.29f;
                var button = CreateButton($"Slot_{type}_{s}", row, out var slotLabel, out var slotImage, EmptySlotColor, 20f);
                Stretch(button.GetComponent<RectTransform>(), new Vector2(x0, 0.04f), new Vector2(x0 + 0.27f, 0.66f));

                var capturedType = type;
                int capturedIndex = s;
                button.onClick.AddListener(() => OnSlotClicked(capturedType, capturedIndex));

                views[s] = new SlotView { image = slotImage, label = slotLabel };
            }
            slotViews[type] = views;
        }
    }

    private void BuildInventoryPanel(RectTransform root)
    {
        var panel = CreateRect("InventoryPanel", root);
        Stretch(panel, new Vector2(0.52f, 0.44f), new Vector2(0.98f, 0.90f));
        panel.gameObject.AddComponent<Image>().color = PanelColor;

        var header = CreateText("InventoryHeader", panel, "所持品（クリックで選択 → スロットをクリックで装備）", 22f, TextAlignmentOptions.MidlineLeft, new Color(0.8f, 0.8f, 0.9f), false);
        Stretch(header.rectTransform, new Vector2(0.02f, 0.92f), new Vector2(0.98f, 1f));

        var scrollRect = CreateRect("InventoryScroll", panel);
        Stretch(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 0.92f));
        scrollRect.offsetMin = new Vector2(10f, 10f);
        scrollRect.offsetMax = new Vector2(-10f, 0f);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();

        var viewport = CreateRect("Viewport", scrollRect);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f); // ドラッグスクロールを受けるための透明な当たり判定

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        inventoryContent = content;
    }

    private void BuildNextFloorPanel(RectTransform root)
    {
        var panel = CreateRect("NextFloorInfoPanel", root);
        Stretch(panel, new Vector2(0.52f, 0.10f), new Vector2(0.98f, 0.42f));
        panel.gameObject.AddComponent<Image>().color = PanelColor;

        // 左側：テキスト情報
        nextFloorInfoText = CreateText("NextFloorInfoText", panel, "", 22f, TextAlignmentOptions.TopLeft, Color.white, true);
        Stretch(nextFloorInfoText.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 1f));
        nextFloorInfoText.rectTransform.offsetMin = new Vector2(16f, 10f);
        nextFloorInfoText.rectTransform.offsetMax = new Vector2(-8f, -10f);

        BuildNextFloorMap(panel);
    }

    // 右側：次の階の盤面の簡易図。y が大きいほど上（実際の盤面も、カメラのyaw=0では
    // z（=y）が大きい側が画面奥＝上に見えるため、入れ替えは不要）。
    private void BuildNextFloorMap(RectTransform panel)
    {
        int width = bootstrapper.BoardWidth;
        int height = bootstrapper.BoardHeight;

        var area = CreateRect("NextFloorMapArea", panel);
        Stretch(area, new Vector2(0.70f, 0.16f), new Vector2(0.99f, 0.97f));

        var map = CreateRect("NextFloorMap", area);
        Stretch(map, Vector2.zero, Vector2.one);
        var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        mapCells = new Image[width, height];
        mapLabels = new TextMeshProUGUI[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = CreateRect($"MapCell_{x}_{y}", map);
                Stretch(cell, new Vector2((float)x / width, (float)y / height),
                              new Vector2((x + 1f) / width, (y + 1f) / height));
                cell.offsetMin = new Vector2(1f, 1f);
                cell.offsetMax = new Vector2(-1f, -1f);

                var image = cell.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                mapCells[x, y] = image;

                var label = CreateText("Label", cell, "", 22f, TextAlignmentOptions.Center, Color.white, true);
                label.fontSizeMin = 8f;
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                mapLabels[x, y] = label;
            }
        }

        // 感知レベル1以下：図全体を暗く覆い、配置が未判明であることを示す
        var overlay = CreateRect("MapLockedOverlay", map);
        Stretch(overlay, Vector2.zero, Vector2.one);
        var overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.7f);
        overlayImage.raycastTarget = false;

        var overlayText = CreateText("MapLockedText", overlay, "？\n<size=60%>配置は未判明</size>", 40f, TextAlignmentOptions.Center, Color.white, false);
        Stretch(overlayText.rectTransform, Vector2.zero, Vector2.one);
        mapLockedOverlay = overlay.gameObject;

        var legend = CreateText("NextFloorMapLegend", panel, "ボ=ボス ス=スカウト 逃=逃げる敵 王=キング", 14f,
            TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.8f), true);
        Stretch(legend.rectTransform, new Vector2(0.70f, 0.01f), new Vector2(0.99f, 0.15f));
    }

    private void RefreshMap(int level)
    {
        if (mapCells == null) return;

        int width = bootstrapper.BoardWidth;
        int height = bootstrapper.BoardHeight;
        var king = bootstrapper.KingStartPosition;
        int zoneHeight = bootstrapper.DeploymentZoneHeight;

        // 敵のマーカーは感知レベル2以上のときだけ描く
        var enemyAt = new Dictionary<Vector2Int, EnemyArchetype>();
        var plan = RunState.NextEncounter;
        if (level >= 2 && plan != null && plan.enemies != null)
        {
            foreach (var enemy in plan.enemies)
                enemyAt[enemy.position] = enemy.archetype;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector2Int(x, y);
                Color color;
                string text = "";
                Color textColor = Color.white;

                if (y < zoneHeight)
                    color = new Color(0.18f, 0.28f, 0.45f);
                else
                    color = (x + y) % 2 == 0 ? new Color(0.22f, 0.22f, 0.28f) : new Color(0.16f, 0.16f, 0.21f);

                if (pos == king)
                {
                    color = new Color(1f, 0.85f, 0.3f);
                    text = "王";
                    textColor = new Color(0.1f, 0.1f, 0.1f);
                }
                else if (enemyAt.TryGetValue(pos, out var archetype))
                {
                    switch (archetype)
                    {
                        case EnemyArchetype.Boss:      color = new Color(0.85f, 0.2f, 0.2f);  text = "ボ"; break;
                        case EnemyArchetype.Scout:     color = new Color(0.9f, 0.55f, 0.15f); text = "ス"; break;
                        case EnemyArchetype.Retreater: color = new Color(0.9f, 0.85f, 0.2f);  text = "逃"; break;
                    }
                    textColor = archetype == EnemyArchetype.Retreater ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
                }

                mapCells[x, y].color = color;
                mapLabels[x, y].text = text;
                mapLabels[x, y].color = textColor;
            }
        }

        mapLockedOverlay.SetActive(level < 2);
    }

    private void BuildNextBattleButton(RectTransform root)
    {
        var button = CreateButton("NextBattleButton", root, out var label, out _, PrimaryButtonColor, 32f);
        Stretch(button.GetComponent<RectTransform>(), new Vector2(0.70f, 0.015f), new Vector2(0.98f, 0.085f));
        label.text = "次の階へ";
        label.alignment = TextAlignmentOptions.Center;

        // RunStateは静的に維持されるため、シーンを再読み込みするだけで次の階に進める
        button.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
    }

    // ---------------------------------------------------------------
    // 操作
    // ---------------------------------------------------------------

    private void OnInventoryItemClicked(EquipmentInstance item)
    {
        selectedItem = selectedItem == item ? null : item;
        Refresh();
    }

    private void OnSlotClicked(PieceType type, int slotIndex)
    {
        if (selectedItem != null)
        {
            if (RunState.TryEquip(type, slotIndex, selectedItem))
                selectedItem = null;
        }
        else
        {
            RunState.Unequip(type, slotIndex);
        }

        Refresh();
    }

    // ---------------------------------------------------------------
    // 表示更新
    // ---------------------------------------------------------------

    private void Refresh()
    {
        foreach (var type in PieceOrder)
        {
            statTexts[type].text = BuildStatText(type);

            var equipped = RunState.GetEquipped(type);
            for (int s = 0; s < RunState.SlotsPerPiece; s++)
            {
                var item = s < equipped.Count ? equipped[s] : null;
                slotViews[type][s].label.text = FormatSlot(type, item);
                slotViews[type][s].image.color = item == null ? EmptySlotColor : ButtonColor;
            }
        }

        RefreshInventory();
        nextFloorInfoText.text = BuildNextFloorInfo();
        RefreshMap(Mathf.Clamp(bootstrapper.GetSenseLevel(), 0, RunState.MaxSenseLevel));
    }

    private void RefreshInventory()
    {
        foreach (var go in inventoryItemObjects)
        {
            if (go == null) continue;
            go.SetActive(false); // Destroyは遅延されるため、レイアウトに二重に数えられないよう先に無効化する
            Destroy(go);
        }
        inventoryItemObjects.Clear();

        // 今回の戦利品（NEW）を先頭に、それ以外は元の順序のまま
        var ordered = new List<EquipmentInstance>();
        foreach (var item in RunState.Inventory.Items)
            if (IsNew(item)) ordered.Add(item);
        foreach (var item in RunState.Inventory.Items)
            if (!IsNew(item)) ordered.Add(item);

        if (ordered.Count == 0)
        {
            var empty = CreateText("InventoryEmptyText", inventoryContent, "（所持品なし）", 24f, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.7f), false);
            AddLayoutHeight(empty.gameObject, 60f);
            inventoryItemObjects.Add(empty.gameObject);
            return;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            var color = item == selectedItem ? SelectedColor : ButtonColor;

            var button = CreateButton($"InventoryItem_{i}", inventoryContent, out var label, out _, color, 22f);
            AddLayoutHeight(button.gameObject, 76f);

            string prefix = IsNew(item) ? "<color=#FFE060><b>NEW</b></color> " : "";
            label.text = prefix + FormatItem(item);

            var captured = item;
            button.onClick.AddListener(() => OnInventoryItemClicked(captured));

            inventoryItemObjects.Add(button.gameObject);
        }
    }

    private bool IsNew(EquipmentInstance item) => item != null && newItemIds.Contains(item.itemId);

    // 「攻撃力 {基礎}→{装備込み} / 行動回数 1→{1+BonusActions合計}」（ナイトのみ距離ボーナスも）
    private string BuildStatText(PieceType type)
    {
        int baseAttack = bootstrapper.GetPlayerBaseAttack(type);

        float attackFlat = 0f;
        float bonusActions = 0f;
        float distanceRate = 0f;

        foreach (var item in RunState.GetEquipped(type))
        {
            if (item == null || item.bonuses == null) continue;

            foreach (var bonus in item.bonuses)
            {
                switch (bonus.statType)
                {
                    case EquipmentStatType.AttackFlat:        attackFlat += bonus.value; break;
                    case EquipmentStatType.BonusActions:      bonusActions += bonus.value; break;
                    case EquipmentStatType.DistanceBonusRate: distanceRate += bonus.value; break;
                }
            }
        }

        int finalAttack = baseAttack + Mathf.RoundToInt(attackFlat);
        int finalActions = 1 + (int)bonusActions; // PieceSnapshotFactory.ComputeMaxActionsと同じ計算

        string text = $"攻撃力 {baseAttack}→{finalAttack} / 行動回数 1→{finalActions}";

        if (type == PieceType.Knight)
        {
            float baseRate = bootstrapper.GetKnightBaseDistanceBonusRate();
            text += $"\n距離ボーナス {Mathf.RoundToInt(baseRate * 100f)}%→{Mathf.RoundToInt((baseRate + distanceRate) * 100f)}%";
        }

        return text;
    }

    // 「[Rare] 鋼の指輪  ATK+2 …」。レアリティの色はBoardDropMarkerと共通。
    private static string FormatItem(EquipmentInstance item)
    {
        string hex = ColorUtility.ToHtmlStringRGB(BoardDropMarker.GetRarityColor(item.rarity));

        var effects = new StringBuilder();
        if (item.bonuses != null)
        {
            foreach (var bonus in item.bonuses)
            {
                if (effects.Length > 0) effects.Append(' ');
                effects.Append("<nobr>").Append(BattleEquipmentGenerator.FormatBonus(bonus)).Append("</nobr>");
            }
        }

        return $"<color=#{hex}>[{item.rarity}] {item.itemName}</color>  {effects}";
    }

    private static string FormatSlot(PieceType type, EquipmentInstance item)
    {
        if (item == null) return "<color=#808898>（空き）</color>";

        string text = FormatItem(item);
        if (type != PieceType.Knight && IsOnlyDistanceBonus(item))
            text += "\n<color=#FF8080>※この駒では効果なし</color>";

        return text;
    }

    private static bool IsOnlyDistanceBonus(EquipmentInstance item)
    {
        if (item.bonuses == null || item.bonuses.Count == 0) return false;
        foreach (var bonus in item.bonuses)
            if (bonus.statType != EquipmentStatType.DistanceBonusRate) return false;
        return true;
    }

    // 感知レベルに応じて、次の階の敵の情報を開示する（未開示は「？」）
    private string BuildNextFloorInfo()
    {
        const string Unknown = "？ <color=#8890A0>（感知装備で判明）</color>";

        int level = Mathf.Clamp(bootstrapper.GetSenseLevel(), 0, RunState.MaxSenseLevel);
        string senseNote = bootstrapper.IsSenseLevelOverridden
            ? "<color=#FF8080>（確認用に固定中）</color>"
            : "<color=#8890A0>（装備の「感知」の合計。上げるほど次の階の情報が増える）</color>";

        var sb = new StringBuilder();
        sb.Append($"<color=#FFE060><size=120%>次の階: {RunState.CurrentFloor}F</size></color>\n");
        sb.Append($"感知レベル: Lv{level}  {senseNote}\n");

        var plan = RunState.NextEncounter;
        if (plan == null || plan.enemies == null)
        {
            sb.Append("敵の情報なし");
            return sb.ToString();
        }

        sb.Append($"敵の数: {plan.enemies.Count}体\n");
        sb.Append("種類: ").Append(level >= 1 ? BuildComposition(plan) : Unknown).Append('\n');
        sb.Append("配置: ").Append(level >= 2 ? "右の図" : Unknown).Append('\n');
        if (level >= 3)
            sb.Append("詳細:\n").Append(BuildDetails(plan));
        else
            sb.Append("詳細: ").Append(Unknown);

        return sb.ToString();
    }

    private static string BuildComposition(EncounterPlan plan)
    {
        var order = new List<EnemyArchetype>();
        var counts = new Dictionary<EnemyArchetype, int>();

        foreach (var enemy in plan.enemies)
        {
            if (!counts.ContainsKey(enemy.archetype))
            {
                counts[enemy.archetype] = 0;
                order.Add(enemy.archetype);
            }
            counts[enemy.archetype]++;
        }

        var parts = new List<string>();
        foreach (var archetype in order)
            parts.Add($"<nobr>{GetEnemyDisplayName(archetype)}×{counts[archetype]}</nobr>");

        return string.Join(" / ", parts);
    }

    // 同じ種類・同じHPの敵を1行にまとめる。
    // 例：「スカウト×2: HP6 / キング3ダメージ / 追跡 / ドロップ品質 低」
    private string BuildDetails(EncounterPlan plan)
    {
        var order = new List<(EnemyArchetype archetype, int hp)>();
        var counts = new Dictionary<(EnemyArchetype archetype, int hp), int>();

        foreach (var enemy in plan.enemies)
        {
            var key = (enemy.archetype, enemy.hp);
            if (!counts.ContainsKey(key))
            {
                counts[key] = 0;
                order.Add(key);
            }
            counts[key]++;
        }

        var lines = new List<string>();
        foreach (var key in order)
        {
            int kingDamage = bootstrapper.GetEnemyKingDamage(key.archetype);
            string kingText = kingDamage < 0 ? "キング？ダメージ" : $"キング{kingDamage}ダメージ";
            string quality = DropRules.GetQualityTier(key.archetype) >= 3 ? "高" : "低";

            lines.Add($"<nobr>{GetEnemyDisplayName(key.archetype)}×{counts[key]}: HP{key.hp} / {kingText} / {GetEnemyBehaviorName(key.archetype)} / ドロップ品質 {quality}</nobr>");
        }

        return string.Join("\n", lines);
    }

    private static string GetEnemyBehaviorName(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Boss:      return "直進";
            case EnemyArchetype.Scout:     return "追跡";
            case EnemyArchetype.Retreater: return "後退";
            default:                       return "？";
        }
    }

    private static string GetPieceDisplayName(PieceType type)
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

    private static string GetEnemyDisplayName(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Boss:      return "ボス";
            case EnemyArchetype.Scout:     return "スカウト";
            case EnemyArchetype.Retreater: return "逃げる敵";
            default:                       return archetype.ToString();
        }
    }

    // ---------------------------------------------------------------
    // UI生成ヘルパー
    // ---------------------------------------------------------------

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

    private static void AddLayoutHeight(GameObject go, float height)
    {
        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
        TextAlignmentOptions alignment, Color color, bool autoSize)
    {
        var rt = CreateRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.richText = true;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (autoSize)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(12f, size * 0.6f);
            tmp.fontSizeMax = size;
        }
        tmp.text = text;
        return tmp;
    }

    private Button CreateButton(string name, Transform parent, out TextMeshProUGUI label, out Image image,
        Color color, float fontSize)
    {
        var rt = CreateRect(name, parent);
        image = rt.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateText("Label", rt, "", fontSize, TextAlignmentOptions.MidlineLeft, Color.white, true);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(10f, 6f);
        label.rectTransform.offsetMax = new Vector2(-10f, -6f);

        return button;
    }
}
