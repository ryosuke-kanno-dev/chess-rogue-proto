using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Chebyss.Battle;

// カーソル下のマスにドロップがあれば、TooltipUIで名前・効果を表示する。
// PieceTooltipTriggerの表示を壊さないよう、自分が表示したツールチップだけをHideする。
public class DropHoverController : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private BoardGenerator boardGenerator; // Initializeの署名上受け取る。マス解決はBoardTile.GridPositionで足りるため現状は未使用
    private Camera mainCamera;

    // 自分が現在表示中のドロップID（-1 = 何も表示していない）
    private int shownDropId = -1;
    private bool warnedMissingTooltip;

    // ドロップの上から外れても、この秒数だけはツールチップを消さない（点滅の防止）
    private const float HideGraceSeconds = 0.12f;
    private float hideDeadline = -1f;

    private bool tooltipPrepared;

    public void Initialize(BattleBootstrapper bootstrapper, BoardGenerator boardGenerator)
    {
        this.bootstrapper = bootstrapper;
        this.boardGenerator = boardGenerator;
    }

    void Update()
    {
        // 設定画面を開いている間は、ツールチップを出さない
        if (BattleSettingsPanelUI.IsOpen)
        {
            HideOwnTooltip();
            return;
        }

        var turnController = bootstrapper != null ? bootstrapper.TurnController : null;
        var keyboardCursor = bootstrapper != null ? bootstrapper.KeyboardCursor : null;
        bool useKeyboard = keyboardCursor != null && keyboardCursor.IsActive;

        if (turnController == null || (!useKeyboard && Mouse.current == null))
        {
            HideOwnTooltip();
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2Int cell;
        Vector2 screenPos;

        if (useKeyboard)
        {
            // キーボードカーソルが有効な間は、カーソルのマスのワールド位置を画面座標にしてマウス位置の代わりにする
            cell = keyboardCursor.Cell;
            var tile = boardGenerator != null ? boardGenerator.GetTile(cell) : null;
            if (tile == null)
            {
                ScheduleHide();
                return;
            }
            screenPos = mainCamera.WorldToScreenPoint(tile.transform.position);
        }
        else
        {
            // UI上ではドロップ判定を行わない（猶予の後に自分の表示を消す）
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ScheduleHide();
                return;
            }

            if (!TryResolveCellUnderCursor(out cell))
            {
                ScheduleHide();
                return;
            }
            screenPos = Mouse.current.position.ReadValue();
        }

        if (!turnController.TryGetDropAt(cell, out var drop))
        {
            ScheduleHide();
            return;
        }

        if (TooltipUI.Instance == null)
        {
            if (!warnedMissingTooltip)
            {
                warnedMissingTooltip = true;
                Debug.LogWarning("[DropHoverController] TooltipUI がシーンに見つかりません。ドロップのツールチップは表示されません（拾得など他の動作には影響しません）。");
            }
            return;
        }

        PrepareTooltipUI();

        // ドロップの上にいる間は、消す予定を取り消す（猶予の間に戻った場合は消さず、再表示もしない）
        hideDeadline = -1f;

        if (shownDropId != drop.dropId)
        {
            // 別のドロップに移った場合は、猶予なしで新しい内容に切り替える
            shownDropId = drop.dropId;
            TooltipUI.Instance.Show(BuildName(drop.item), BuildDescription(drop, bootstrapper.GetAppraisalLevel()), screenPos);
        }
        else
        {
            // 同じドロップの上にいる間は、位置だけを更新する
            TooltipUI.Instance.UpdatePosition(screenPos);
        }
    }

    void OnDisable()
    {
        HideOwnTooltip();
    }

    // ドロップの上から外れたとき：猶予（HideGraceSeconds）が過ぎるまで待ってから消す
    private void ScheduleHide()
    {
        if (shownDropId < 0) return;

        if (hideDeadline < 0f)
        {
            hideDeadline = Time.unscaledTime + HideGraceSeconds;
            return;
        }

        if (Time.unscaledTime >= hideDeadline) HideOwnTooltip();
    }

    // ツールチップがクリックやホバーの判定（IsPointerOverGameObject）を受け取らないようにする。
    // TooltipUI（Legacy）は変更せず、その配下を検索して一度だけ設定する。
    // 他のボタン等（Selectable）を含む部分には触れない。
    private void PrepareTooltipUI()
    {
        if (tooltipPrepared) return;
        tooltipPrepared = true;

        var root = TooltipUI.Instance.transform;

        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponentInParent<Selectable>() != null) continue;
            graphic.raycastTarget = false;
        }

        // panelRootはprivateのため、TooltipUIの配下から探す。
        // TooltipUI自身と、その直下の子（panelRootの候補）に、レイキャストを受けないCanvasGroupを付ける。
        DisableRaycastsWithCanvasGroup(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
            DisableRaycastsWithCanvasGroup(root.GetChild(i).gameObject);
    }

    private static void DisableRaycastsWithCanvasGroup(GameObject target)
    {
        if (target.GetComponentInChildren<Selectable>(true) != null) return;

        var group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void HideOwnTooltip()
    {
        hideDeadline = -1f;
        if (shownDropId < 0) return;
        shownDropId = -1;
        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }

    // BattleInputController.TryResolveClickedCellと同じ方式：タイルでも駒の3Dモデルでも、
    // 盤面上のどのマスを指しているかに統一して解決する。
    private bool TryResolveCellUnderCursor(out Vector2Int cell)
    {
        cell = default;
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) return false;

        var tile = hit.collider.GetComponent<BoardTile>();
        if (tile != null)
        {
            cell = tile.GridPosition;
            return true;
        }

        var link = hit.collider.GetComponentInParent<PieceBoardLink>();
        if (link != null)
        {
            cell = link.GridPosition;
            return true;
        }

        return false;
    }

    private static string BuildName(EquipmentInstance item)
    {
        return $"[{item.rarity}] {item.itemName}";
    }

    // 効果の一覧（1件ごとに<nobr>で囲み、件の途中では折り返さない）
    private static string FormatEffects(EquipmentInstance item)
    {
        var sb = new StringBuilder();
        if (item.bonuses != null)
        {
            foreach (var bonus in item.bonuses)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append("<nobr>").Append(BattleEquipmentGenerator.FormatBonus(bonus)).Append("</nobr>");
            }
        }
        return sb.ToString();
    }

    // 通常版の効果に続けて、拾得ボーナス（+1版）の内容を表示する
    private static string BuildDescription(DropEntry drop, int appraisalLevel)
    {
        var sb = new StringBuilder();
        sb.Append(FormatEffects(drop.item)).Append('\n');

        if (drop.boostedItem != null)
        {
            var boosted = drop.boostedItem;

            // 鑑定レベルによって、拾った後の装備の情報の開示を切り替える
            string preview;
            if (appraisalLevel >= 2)
                preview = $"[{boosted.rarity}] {boosted.itemName}　{FormatEffects(boosted)}";
            else if (appraisalLevel == 1)
                preview = $"[{boosted.rarity}] ？？？";
            else
                preview = "？（鑑定スキルで判明）";

            sb.Append("<color=#FFE060>拾うと：</color>")
              .Append(preview)
              .Append('\n')
              .Append("駒を止めると拾得（勝利時の自動回収は通常版のまま）");
        }
        else
        {
            sb.Append("最高ランクのため、拾得ボーナスはありません");
        }

        return sb.ToString();
    }
}
