using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Chebyss.Battle;

public class BattleInputController : MonoBehaviour
{
    [SerializeField] private BattleBootstrapper bootstrapper;
    [SerializeField] private TileHighlightController highlightController;
    [SerializeField] private BoardGenerator boardGenerator;
    [SerializeField] private EnemyIntentDisplay intentDisplay;

    private Camera mainCamera;
    private int lastSeenTurn;

    // 選択中の駒のグリッド座標（未選択ならnull）
    private Vector2Int? selectedPos;

    // ナイトの追撃移動を選択中の状態
    private Vector2Int? pursuingKnightPos;
    private List<Vector2Int> pursuitCandidates;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        var turnController = bootstrapper.TurnController;
        if (turnController == null) return;

        var board = bootstrapper.Board;
        if (board.CurrentTurn != lastSeenTurn)
        {
            // ターンが進んだら、選択・追撃移動待ちの状態を破棄する
            lastSeenTurn = board.CurrentTurn;
            CancelInteraction();
        }

        if (turnController.CurrentPhase != BattlePhase.PlayerTurn) return;
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (!TryResolveClickedCell(out var cell)) return;

        if (pursuingKnightPos.HasValue)
        {
            HandlePursuitClick(cell, turnController);
            return;
        }

        HandleCellClick(cell, turnController);
    }

    // クリックしたものが「タイル」でも「駒の3Dモデル」でも、盤面上のどのマスを
    // 指しているかに統一して解決する。駒のコライダーがタイルへのクリックを横取りして
    // 敵を攻撃できなくなる問題を防ぐ。
    private bool TryResolveClickedCell(out Vector2Int cell)
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

    private void HandleCellClick(Vector2Int cell, BattleTurnController turnController)
    {
        var board = bootstrapper.Board;

        if (selectedPos.HasValue)
        {
            var from = selectedPos.Value;

            if (cell == from)
            {
                // 選択中の駒自身を再クリック → 待機
                turnController.Wait(from);
                Deselect();
                return;
            }

            if (turnController.GetAttackableTiles(from).Contains(cell))
            {
                var attackResult = turnController.ExecuteAttack(from, cell, out var outcome);
                Debug.Log($"[BattleInputController] 攻撃結果: damage={outcome.damage}, defeated={outcome.targetDefeated}, effects={outcome.appliedEffects.Count}");
                bootstrapper.VisualSync.SyncAll(board);

                if (attackResult == ActionResult.Success && outcome.PursuitMoveAvailable)
                {
                    BeginPursuitSelection(from, outcome.pursuitMoveCandidates);
                    return;
                }

                Deselect();
                return;
            }

            if (turnController.GetMovableTiles(from).Contains(cell))
            {
                var result = turnController.ExecuteMove(from, cell);
                if (result == ActionResult.Success) bootstrapper.VisualSync.SyncAll(board);
                Deselect();
                return;
            }
            // どちらにも該当しない場合は、別の自駒をクリックしたなら選択を切り替える
        }

        if (IsSelectablePlayerPiece(board, cell))
        {
            Select(cell, turnController);
            return;
        }

        Deselect();
    }

    private static bool IsSelectablePlayerPiece(BoardState board, Vector2Int cell)
    {
        if (!board.TryGetPlayerPiece(cell, out var piece)) return false;

        // キングは現状行動しない（「王の移動権」アイテム実装後に対象へ加える）
        if (piece.pieceType == PieceType.King) return false;

        // 行動回数を使い切った駒は、選択しても何もできないため選択させない
        return piece.actionsLeft > 0;
    }

    private void BeginPursuitSelection(Vector2Int knightPos, IReadOnlyList<Vector2Int> candidates)
    {
        highlightController.ClearHighlights();
        intentDisplay.ClearHighlights();
        selectedPos = null;
        pursuingKnightPos = knightPos;
        pursuitCandidates = new List<Vector2Int>(candidates);

        var turnController = bootstrapper.TurnController;
        foreach (var pos in pursuitCandidates)
        {
            var type = turnController != null && turnController.HasDropAt(pos)
                ? HighlightType.MovableWithDrop
                : HighlightType.Movable;
            boardGenerator.GetTile(pos)?.SetHighlight(type);
        }
    }

    private void HandlePursuitClick(Vector2Int cell, BattleTurnController turnController)
    {
        if (pursuitCandidates.Contains(cell))
        {
            turnController.ExecutePursuitMove(pursuingKnightPos.Value, cell);
            bootstrapper.VisualSync.SyncAll(bootstrapper.Board);
        }

        // 候補外をクリックした場合は追撃移動を辞退（その場に留まる）
        CancelInteraction();
    }

    private void Select(Vector2Int cell, BattleTurnController turnController)
    {
        selectedPos = cell;
        intentDisplay.ClearHighlights();
        highlightController.ShowHighlightsFor(turnController, cell);
    }

    private void Deselect()
    {
        selectedPos = null;
        highlightController.ClearHighlights();
        RefreshIntents();
    }

    private void RefreshIntents()
    {
        var turnController = bootstrapper.TurnController;
        if (turnController != null && turnController.CurrentPhase == BattlePhase.PlayerTurn)
            intentDisplay.UpdateIntents(bootstrapper.Board, turnController.EnemyTurnProcessor);
    }

    private void CancelInteraction()
    {
        if (pursuitCandidates != null)
        {
            foreach (var pos in pursuitCandidates)
                boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        }

        pursuingKnightPos = null;
        pursuitCandidates = null;
        Deselect();
    }
}
