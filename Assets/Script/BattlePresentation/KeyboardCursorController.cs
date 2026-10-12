using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Chebyss.Battle;

// キーボードだけで遊ぶためのマスのカーソルと、その操作（移動・決定・キャンセル・Tab・Space）。
// 矢印キー／WASD＝移動、Enter＝決定、Esc／Backspace＝キャンセル、Tab（Shift+Tab）＝次の駒（前の駒）、
// Space＝ターン終了・配置完了。マウスが動く／押されるとカーソルを隠し、キー入力で再表示する。
public class KeyboardCursorController : MonoBehaviour
{
    private const float RepeatDelaySeconds = 0.25f;
    private const float RepeatIntervalSeconds = 0.08f;
    private const float MouseMoveThresholdPixels = 2f;

    private static readonly Color CursorColor = new Color(0.2f, 0.95f, 1.0f);

    private BattleBootstrapper bootstrapper;
    private BoardGenerator boardGenerator;
    private PlacementRangePreview preview;

    public bool IsActive { get; private set; }
    public Vector2Int Cell { get; private set; }

    private bool hasLastCell;
    private bool lastWasPlacement = true;
    private Vector2 mouseAnchor;

    // 長押しリピート用
    private Vector2Int heldDirection;
    private float nextRepeatTime;

    // カーソルの見た目
    private GameObject cursorRoot;

    // 配置フェーズ：自分が表示させたプレビューだけを消すためのフラグ
    private bool isShowingPreview;

    private BattleInputController battleInput;

    public void Initialize(BattleBootstrapper bootstrapper, BoardGenerator generator, PlacementRangePreview preview)
    {
        this.bootstrapper = bootstrapper;
        this.boardGenerator = generator;
        this.preview = preview;
        BuildCursor();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || bootstrapper == null)
        {
            SetActive(false);
            return;
        }

        var turnController = bootstrapper.TurnController;
        bool placement = bootstrapper.IsPlacementPhase;
        bool playerTurn = !placement && turnController != null && turnController.CurrentPhase == BattlePhase.PlayerTurn;

        // 操作できない局面（敵ターン・勝敗確定後など）ではカーソルを隠す
        if (!placement && !playerTurn)
        {
            SetActive(false);
            ClearOwnPreview();
            return;
        }

        if (placement != lastWasPlacement)
        {
            lastWasPlacement = placement;
            hasLastCell = false; // 配置フェーズ→バトルで、カーソル位置を取り直す
            SetActive(false);
        }

        // マウスでクリックしたボタンの選択状態が残り、Enter／Spaceで二重に実行されるのを防ぐ
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        if (battleInput == null) battleInput = FindFirstObjectByType<BattleInputController>();

        HandleMouseInput();

        Vector2Int screenDirection = ReadMoveInput(keyboard);
        bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        bool cancel = keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame;
        bool tab = keyboard.tabKey.wasPressedThisFrame;
        bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        bool space = keyboard.spaceKey.wasPressedThisFrame;

        bool anyCursorInput = screenDirection != Vector2Int.zero || confirm || cancel || tab;
        bool wasActive = IsActive;

        if (anyCursorInput && !wasActive)
            SetActive(true);

        // 非表示から復帰した最初の入力は、移動・決定では「カーソルを表示するだけ」にする
        // （見えていない位置で意図せず決定してしまわないため）
        if (screenDirection != Vector2Int.zero && wasActive) MoveCursor(screenDirection);

        if (placement)
        {
            // 駒が未選択なら、非表示からの最初のTabでも最初の駒を選択する。
            // 選択中なら、非表示からの最初のTabはカーソルを表示するだけにする。
            var placementInput = bootstrapper.PlacementInput;
            bool hasSelection = placementInput != null && placementInput.SelectedType.HasValue;
            bool tabForPlacement = tab && (wasActive || !hasSelection);

            HandlePlacementInput(confirm && wasActive, cancel, tabForPlacement, shift, space);
        }
        else
        {
            // バトル中：非表示からの最初のTabは、カーソルを表示するだけ（巡回しない）
            HandleBattleInput(confirm && wasActive, cancel, tab && wasActive, shift, space);
        }

        UpdateCursorVisual();
        if (placement) UpdatePlacementPreview();
    }

    void OnDisable()
    {
        ClearOwnPreview();
    }

    // ---------------------------------------------------------------
    // バトル中（プレイヤーターン）
    // ---------------------------------------------------------------

    private void HandleBattleInput(bool confirm, bool cancel, bool tab, bool shift, bool space)
    {
        if (battleInput == null) return;

        if (confirm) battleInput.RequestActivateCell(Cell);
        if (cancel) battleInput.RequestCancel();

        if (tab)
        {
            if (battleInput.HasSelection || battleInput.IsPursuing)
                CycleCell(battleInput.GetTargetCells(), shift);
            else
                CycleCell(GetActionablePieceCells(), shift);
        }

        if (space) bootstrapper.AdvancePhase(); // ターン終了
    }

    // 行動できる駒（キングを除く）のマス。盤面の座標順。
    private List<Vector2Int> GetActionablePieceCells()
    {
        var cells = new List<Vector2Int>();
        var board = bootstrapper.Board;

        foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
        {
            if (board.TryGetPlayerPiece(pos, out var piece) && piece.pieceType != PieceType.King && piece.actionsLeft > 0)
                cells.Add(pos);
        }
        return cells;
    }

    // ---------------------------------------------------------------
    // 配置フェーズ
    // ---------------------------------------------------------------

    private void HandlePlacementInput(bool confirm, bool cancel, bool tab, bool shift, bool space)
    {
        var placementInput = bootstrapper.PlacementInput;
        if (placementInput == null) return;

        if (tab) CycleSelectedPiece(placementInput, shift);

        if (confirm)
        {
            if (placementInput.SelectedType.HasValue &&
                bootstrapper.Placement.IsInDeploymentZone(Cell) && !bootstrapper.Board.IsOccupied(Cell))
            {
                // 空きマスなら、選択中の駒をここに置く
                var type = placementInput.SelectedType.Value;
                if (bootstrapper.TryGetDraggable(type, out var draggable) && draggable.PlaceAt(Cell))
                    placementInput.Deselect();
            }
            else if (TryGetRosterTypeAt(Cell, out var typeAtCell))
            {
                // 名簿の駒がいるマスなら、その駒を選択する
                placementInput.Select(typeAtCell);
            }
        }

        if (cancel) placementInput.Deselect();

        if (space && bootstrapper.Placement != null && bootstrapper.Placement.IsComplete)
            bootstrapper.AdvancePhase(); // 配置完了
    }

    // 名簿の駒を順に選択する。選択した駒が配置済みなら、カーソルをその駒のマスへ移す。
    private void CycleSelectedPiece(PlacementInputController placementInput, bool backward)
    {
        var roster = bootstrapper.RosterTypes;
        if (roster.Count == 0) return;

        int index = -1;
        if (placementInput.SelectedType.HasValue)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] == placementInput.SelectedType.Value) { index = i; break; }
        }

        int next;
        if (index < 0) next = backward ? roster.Count - 1 : 0;
        else next = (index + (backward ? -1 : 1) + roster.Count) % roster.Count;

        var type = roster[next];
        placementInput.Select(type);

        if (TryFindPlacedCell(type, out var placedCell))
        {
            Cell = placedCell;
            hasLastCell = true;
        }
    }

    private bool TryFindPlacedCell(PieceType type, out Vector2Int cell)
    {
        cell = default;
        var board = bootstrapper.Board;

        foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
        {
            if (board.TryGetPlayerPiece(pos, out var piece) && piece.pieceType == type)
            {
                cell = pos;
                return true;
            }
        }
        return false;
    }

    private bool TryGetRosterTypeAt(Vector2Int cell, out PieceType type)
    {
        type = default;
        if (!bootstrapper.Board.TryGetPlayerPiece(cell, out var piece)) return false;

        foreach (var rosterType in bootstrapper.RosterTypes)
        {
            if (rosterType == piece.pieceType)
            {
                type = rosterType;
                return true;
            }
        }
        return false;
    }

    // 駒を選択中でカーソルが表示されている間は、そのマスでの範囲をプレビューする
    private void UpdatePlacementPreview()
    {
        var placementInput = bootstrapper.PlacementInput;
        if (preview == null || placementInput == null || !IsActive || !placementInput.SelectedType.HasValue)
        {
            ClearOwnPreview();
            return;
        }

        preview.ShowFor(placementInput.SelectedType.Value, Cell);
        isShowingPreview = true;
    }

    private void ClearOwnPreview()
    {
        if (!isShowingPreview) return;
        isShowingPreview = false;
        if (preview != null) preview.Clear();
    }

    // ---------------------------------------------------------------
    // 入力・カーソル移動
    // ---------------------------------------------------------------

    // マウスが2px以上動く、またはボタンが押されたらカーソルを隠す
    private void HandleMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null || !IsActive) return;

        bool clicked = mouse.leftButton.wasPressedThisFrame ||
                       mouse.rightButton.wasPressedThisFrame ||
                       mouse.middleButton.wasPressedThisFrame;
        bool moved = Vector2.Distance(mouse.position.ReadValue(), mouseAnchor) >= MouseMoveThresholdPixels;

        if (clicked || moved) SetActive(false);
    }

    private void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;

        if (active)
        {
            if (Mouse.current != null) mouseAnchor = Mouse.current.position.ReadValue();
            if (!hasLastCell) InitializeCellPosition();
        }

        if (cursorRoot != null) cursorRoot.SetActive(active);
    }

    // 最初の入力：長押しは0.25秒後から0.08秒ごとに繰り返す。戻り値は「画面上の方向」。
    private Vector2Int ReadMoveInput(Keyboard keyboard)
    {
        Vector2Int pressed = Vector2Int.zero;
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) pressed = Vector2Int.up;
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) pressed = Vector2Int.down;
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) pressed = Vector2Int.left;
        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) pressed = Vector2Int.right;

        if (pressed != Vector2Int.zero)
        {
            heldDirection = pressed;
            nextRepeatTime = Time.unscaledTime + RepeatDelaySeconds;
            return pressed;
        }

        if (heldDirection != Vector2Int.zero)
        {
            if (!IsDirectionHeld(keyboard, heldDirection))
            {
                heldDirection = Vector2Int.zero;
            }
            else if (Time.unscaledTime >= nextRepeatTime)
            {
                nextRepeatTime = Time.unscaledTime + RepeatIntervalSeconds;
                return heldDirection;
            }
        }

        return Vector2Int.zero;
    }

    private static bool IsDirectionHeld(Keyboard keyboard, Vector2Int direction)
    {
        if (direction == Vector2Int.up) return keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
        if (direction == Vector2Int.down) return keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
        if (direction == Vector2Int.left) return keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
        return keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;
    }

    private void MoveCursor(Vector2Int screenDirection)
    {
        var next = Cell + ScreenToBoardDelta(screenDirection);
        if (!IsCursorCell(next)) return;

        Cell = next;
        hasLastCell = true;
    }

    // カーソルの範囲：バトル中は盤面全体、配置フェーズは自陣エリア内
    private bool IsCursorCell(Vector2Int cell)
    {
        if (!bootstrapper.Board.IsInBounds(cell)) return false;
        if (bootstrapper.IsPlacementPhase && cell.y >= bootstrapper.DeploymentZoneHeight) return false;
        return true;
    }

    // 画面の上下左右を、カメラの向きに合わせて盤面の軸（x=ワールドX、y=ワールドZ）へ変換する。
    // 既定のカメラ（yaw=0）では 上=y+、右=x+。右クリックドラッグで回転した場合も画面と一致する。
    private static Vector2Int ScreenToBoardDelta(Vector2Int screenDirection)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        var camera = Camera.main;
        if (camera != null)
        {
            var flatForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            var flatRight = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f && flatRight.sqrMagnitude > 0.0001f)
            {
                forward = flatForward.normalized;
                right = flatRight.normalized;
            }
        }

        Vector3 world = right * screenDirection.x + forward * screenDirection.y;

        if (Mathf.Abs(world.x) >= Mathf.Abs(world.z))
            return new Vector2Int(world.x >= 0f ? 1 : -1, 0);
        return new Vector2Int(0, world.z >= 0f ? 1 : -1);
    }

    // リストの中で、現在のカーソルの次（前）のマスへ移す。リストに無ければ先頭（末尾）へ。
    private void CycleCell(IReadOnlyList<Vector2Int> cells, bool backward)
    {
        if (cells == null || cells.Count == 0) return;

        int index = -1;
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] == Cell) { index = i; break; }

        int next;
        if (index < 0) next = backward ? cells.Count - 1 : 0;
        else next = (index + (backward ? -1 : 1) + cells.Count) % cells.Count;

        Cell = cells[next];
        hasLastCell = true;
    }

    // 表示に切り替わった時の初期位置
    private void InitializeCellPosition()
    {
        if (bootstrapper.IsPlacementPhase)
        {
            var placementInput = bootstrapper.PlacementInput;
            if (placementInput != null && placementInput.SelectedType.HasValue &&
                TryFindPlacedCell(placementInput.SelectedType.Value, out var placedCell))
            {
                Cell = placedCell;
            }
            else
            {
                Cell = new Vector2Int(bootstrapper.BoardWidth / 2, 1);
            }
        }
        else
        {
            var actionable = GetActionablePieceCells();
            Cell = actionable.Count > 0 ? actionable[0] : bootstrapper.Board.KingPosition;
        }

        hasLastCell = true;
    }

    // ---------------------------------------------------------------
    // 見た目
    // ---------------------------------------------------------------

    private void BuildCursor()
    {
        cursorRoot = new GameObject("KeyboardCursor");

        const float length = 0.96f;
        const float thickness = 0.06f;
        const float height = 0.02f;
        const float offset = 0.45f;

        CreateBar("Top",    new Vector3(0f, 0f,  offset), new Vector3(length, height, thickness));
        CreateBar("Bottom", new Vector3(0f, 0f, -offset), new Vector3(length, height, thickness));
        CreateBar("Left",   new Vector3(-offset, 0f, 0f), new Vector3(thickness, height, length));
        CreateBar("Right",  new Vector3( offset, 0f, 0f), new Vector3(thickness, height, length));

        cursorRoot.SetActive(false);
    }

    private void CreateBar(string name, Vector3 localPosition, Vector3 localScale)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(cursorRoot.transform, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = localScale;

        // 既存のクリック判定（Raycast）を妨げないよう、Colliderは付けない
        var collider = bar.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        var renderer = bar.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = CursorColor;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // マスの上面から0.05上に置き、周期1秒で±5%拡縮する
    private void UpdateCursorVisual()
    {
        if (cursorRoot == null || !IsActive) return;

        var tile = boardGenerator.GetTile(Cell);
        if (tile == null) return;

        cursorRoot.transform.position = tile.transform.position + Vector3.up * 0.05f;

        float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI);
        cursorRoot.transform.localScale = new Vector3(pulse, 1f, pulse);
    }
}
