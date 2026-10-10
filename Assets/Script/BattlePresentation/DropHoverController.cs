using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    public void Initialize(BattleBootstrapper bootstrapper, BoardGenerator boardGenerator)
    {
        this.bootstrapper = bootstrapper;
        this.boardGenerator = boardGenerator;
    }

    void Update()
    {
        var turnController = bootstrapper != null ? bootstrapper.TurnController : null;
        if (turnController == null || Mouse.current == null)
        {
            HideOwnTooltip();
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // UI上ではドロップ判定を行わない（自分の表示が残っていれば消す）
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HideOwnTooltip();
            return;
        }

        if (!TryResolveCellUnderCursor(out var cell) ||
            !turnController.TryGetDropAt(cell, out var drop))
        {
            HideOwnTooltip();
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

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (shownDropId != drop.dropId)
        {
            shownDropId = drop.dropId;
            TooltipUI.Instance.Show(BuildName(drop.item), BuildDescription(drop.item), mousePos);
        }
        else
        {
            TooltipUI.Instance.UpdatePosition(mousePos);
        }
    }

    void OnDisable()
    {
        HideOwnTooltip();
    }

    private void HideOwnTooltip()
    {
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

    private static string BuildDescription(EquipmentInstance item)
    {
        var sb = new StringBuilder();
        if (item.bonuses != null)
        {
            foreach (var bonus in item.bonuses)
            {
                if (sb.Length > 0) sb.Append(' ');
                // ボーナスの途中で折り返さない（ボーナス同士の区切りの空白では折り返してよい）
                sb.Append("<nobr>").Append(BattleEquipmentGenerator.FormatBonus(bonus)).Append("</nobr>");
            }
        }
        sb.Append('\n').Append("駒を止めると拾得");
        return sb.ToString();
    }
}
