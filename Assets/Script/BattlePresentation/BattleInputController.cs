using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Chebyss.Battle;

public class BattleInputController : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;
    [SerializeField] private TileHighlightController highlightController;
    [SerializeField] private BoardGenerator boardGenerator;

    private Camera mainCamera;
    private PieceBoardLink selectedPieceLink;

    // 撃破によってナイトの追撃移動が選べる状態かどうか
    private PieceBoardLink pursuingPieceLink;
    private List<Vector2Int> pursuitCandidates;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        var turnController = bootstrapper.TurnController;
        if (turnController == null || turnController.CurrentPhase != BattlePhase.PlayerTurn) return;
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var clickedTile = hit.collider.GetComponent<BoardTile>();

        if (pursuingPieceLink != null)
        {
            if (clickedTile != null) HandlePursuitClick(clickedTile.GridPosition, turnController);
            return;
        }

        var clickedLink = hit.transform.GetComponentInParent<PieceBoardLink>();

        if (clickedLink != null)
        {
            HandlePieceClick(clickedLink, turnController);
            return;
        }

        if (clickedTile != null && selectedPieceLink != null)
        {
            HandleTileClick(clickedTile.GridPosition, turnController);
        }
    }

    private void HandlePieceClick(PieceBoardLink clicked, BattleTurnController turnController)
    {
        if (selectedPieceLink == clicked)
        {
            turnController.Wait(clicked.GridPosition);
            Deselect();
            return;
        }

        SelectPiece(clicked, turnController);
    }

    private void HandleTileClick(Vector2Int to, BattleTurnController turnController)
    {
        var from = selectedPieceLink.GridPosition;

        if (to == from)
        {
            turnController.Wait(from);
            Deselect();
            return;
        }

        if (turnController.GetAttackableTiles(from).Contains(to))
        {
            var attackResult = turnController.ExecuteAttack(from, to, out var outcome);
            Debug.Log($"[BattleInputController] 攻撃結果: damage={outcome.damage}, defeated={outcome.targetDefeated}");

            bootstrapper.VisualSync.SyncAll(bootstrapper.Board);

            if (attackResult == ActionResult.Success && outcome.PursuitMoveAvailable)
            {
                BeginPursuitSelection(selectedPieceLink, outcome.pursuitMoveCandidates);
                return;
            }

            Deselect();
            return;
        }

        if (turnController.GetMovableTiles(from).Contains(to))
        {
            var result = turnController.ExecuteMove(from, to);
            if (result == ActionResult.Success)
            {
                bootstrapper.VisualSync.SyncAll(bootstrapper.Board);
            }
            Deselect();
            return;
        }

        Deselect();
    }

    private void BeginPursuitSelection(PieceBoardLink knightLink, IReadOnlyList<Vector2Int> candidates)
    {
        highlightController.ClearHighlights();
        pursuingPieceLink = knightLink;
        pursuitCandidates = new List<Vector2Int>(candidates);

        foreach (var pos in pursuitCandidates)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.Movable);

        selectedPieceLink = null;
    }

    private void HandlePursuitClick(Vector2Int to, BattleTurnController turnController)
    {
        if (pursuitCandidates.Contains(to))
        {
            turnController.ExecutePursuitMove(pursuingPieceLink.GridPosition, to);
            bootstrapper.VisualSync.SyncAll(bootstrapper.Board);
        }

        foreach (var pos in pursuitCandidates)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);

        pursuingPieceLink = null;
        pursuitCandidates = null;
    }

    private void SelectPiece(PieceBoardLink link, BattleTurnController turnController)
    {
        selectedPieceLink = link;
        highlightController.ShowHighlightsFor(turnController, link.GridPosition);
    }

    private void Deselect()
    {
        selectedPieceLink = null;
        highlightController.ClearHighlights();
    }
}
