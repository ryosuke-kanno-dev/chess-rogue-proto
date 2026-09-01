using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PieceDraggable : MonoBehaviour
{
  private Camera mainCamera;
  private bool isDragging = false;
  private Vector3 offset;
  private float startX, startZ;

  // 課題【駒特性ツールチップ】: PieceTooltipTrigger側から「今この駒がドラッグ中かどうか」を
  // 参照するための公開プロパティ。ドラッグ判定ロジック自体（isDraggingフィールドの更新箇所）は一切変更しない。
  public bool IsDragging => isDragging;

  // クリック（単押し）とドラッグ（長押し）を判別するための変数
  private Vector3 mouseDownPos;
  private float mouseDownTime;
  private const float DragThresholdDistance = 14f; // このピクセル数以上動いたらドラッグとみなす（クリック判定はこの距離のみで行う）
  private const float DragThresholdTime = 0.2f;     // この時間以上押したらドラッグとみなす

  void Start()
  {
    mainCamera = Camera.main;
  }

  void Update()
  {
    if (Mouse.current == null) return;

    // ステップ7: カメラ参照が何らかの理由で失われていた場合の防御的再取得
    if (mainCamera == null) mainCamera = Camera.main;
    if (mainCamera == null) return;

    Vector2 mousePos = Mouse.current.position.ReadValue();

    // 課題【合成/融合の手動選択モード】: 選択モード中は通常のドラッグ移動・右クリック操作を一切行わず、
    // 左クリック（マウスダウン+アップ、移動距離が小さい単押し）だけを「選択のトグル」として扱う。
    if (DebugGameManager.Instance != null && DebugGameManager.Instance.UI_IsSelectionModeActive())
    {
      HandleSelectionModeClick(mousePos);
      return; // 既存のドラッグ/選択/右クリック処理は一切実行しない
    }

    // マウス左ボタンを押した瞬間
    if (Mouse.current.leftButton.wasPressedThisFrame)
    {
      // ステップ11: UGUIボタン等の上でのクリックは3D側のドラッグ開始対象にしない（入力の二重処理防止）
      if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      {
        return;
      }

      Ray ray = mainCamera.ScreenPointToRay(mousePos);
      if (Physics.Raycast(ray, out RaycastHit hit))
      {
        if (hit.transform == transform)
        {
          // 課題4【敵駒インスペクト復活】: 押下自体は所有者を問わず常に受け付ける
          // （でなければ、そもそもマウスアップ側のSelectPiece呼び出しに到達できず、
          // 敵駒のステータス確認が一切できなくなってしまう）。
          // 実際に位置を動かせるかどうか（isDragging中の追従・SnapToGrid）は、
          // 下のドラッグ処理側で IsOwnedByLocalPlayer() により個別に制御する。
          mouseDownPos = mousePos;
          mouseDownTime = Time.time;
          isDragging = true;

          startX = transform.position.x;
          startZ = transform.position.z;

          Plane plane = new Plane(Vector3.up, transform.position);
          if (plane.Raycast(ray, out float enter))
          {
            offset = transform.position - ray.GetPoint(enter);
          }
        }
      }
    }

    // ドラッグ中の移動処理（※戦闘中はドラッグ移動禁止／課題4: 敵駒(プレイヤー非所有)も追従させない）
    if (isDragging && Mouse.current.leftButton.isPressed && IsOwnedByLocalPlayer())
    {
      if (DebugGameManager.Instance != null && DebugGameManager.Instance.isBattleStarted)
      {
        // 戦闘中の場合はドラッグ処理を実行しない（単押しクリックの判定へ流す）
      }
      else if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      {
        // ステップ12【強化】: カーソルがUGUI領域（ショップ/ボタン等）に入っている間は
        // 3D側の追従を止める（UIの上に駒が乗り上げて見える／誤操作になるのを防ぐ）
      }
      else
      {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float enter))
        {
          Vector3 hitPoint = ray.GetPoint(enter);
          Vector3 targetPos = hitPoint + offset;

          // ドラッグ中はY軸を少し浮かせる
          transform.position = new Vector3(targetPos.x, 0.6f, targetPos.z);
        }

        // 課題【Indexベースのベンチハイライト】: 現在カーソルがベンチエリア上にあるかどうかを判定し、
        // ベンチエリア上であれば「最も近いベンチスロットのIndex」だけをDebugGameManagerへ通知する。
        // ベンチエリア外（盤面上など）にいる場合は-1を通知してハイライトを消す。
        // 実際にどのスロットを点灯させるか（占有中なら対象外にする等）の判断はDebugGameManager側の
        // SetBenchHoverIndex内で一元的に行うため、ここでは「今どこを指しているか」を伝えるだけでよい。
        if (DebugGameManager.Instance != null)
        {
          bool hoveringBench = DebugGameManager.Instance.IsWorldPositionInBenchArea(transform.position);
          if (hoveringBench)
          {
            int nearestBenchIndex = Mathf.Clamp(
              DebugGameManager.Instance.WorldToNearestBenchIndex(transform.position),
              0, DebugGameManager.Instance.BenchSlotCount - 1);
            DebugGameManager.Instance.SetBenchHoverIndex(nearestBenchIndex);
          }
          else
          {
            DebugGameManager.Instance.SetBenchHoverIndex(-1);
          }
        }
      }
    }

    // マウス左ボタンを離した瞬間（クリックかドラッグかの判定）
    if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
    {
      isDragging = false;

      // 課題【Indexベースのベンチハイライト】: ドラッグ操作が終わったら、ホバー中だったスロットのハイライトのみを消す
      if (DebugGameManager.Instance != null) DebugGameManager.Instance.ClearBenchHover();

      float dist = Vector3.Distance(mouseDownPos, mousePos);

      bool isBattle = DebugGameManager.Instance != null && DebugGameManager.Instance.isBattleStarted;
      bool canDragMove = IsOwnedByLocalPlayer();

      // ステップ7【緊急バグ修正】: 押下時間ではなく移動距離のみで「単押し（選択/インスペクト）」を判定する。
      // 従来は「距離が小さい かつ 時間が0.2秒未満」の両方を要求していたため、
      // 少し長めに押しただけの通常のクリックが誤ってドラッグ確定（SnapToGrid）に流れ、
      // SelectPieceが呼ばれない不具合があった。
      // 課題4【敵駒インスペクト復活】: 敵駒（プレイヤー非所有）はそもそも位置を追従させていないため、
      // ドラッグ距離に関わらず常に「選択（インスペクト）」として扱う。
      if (!canDragMove || isBattle || dist < DragThresholdDistance)
      {
        // 戦闘中でない場合、かつ実際に位置を動かしていた場合のみ、ドラッグで浮いた位置を元の位置（グリッド）に戻す
        if (!isBattle && canDragMove)
        {
          transform.position = new Vector3(startX, 0.25f, startZ);
        }

        if (DebugGameManager.Instance != null)
        {
          DebugGameManager.Instance.SelectPiece(GetComponent<PieceData>());
        }
      }
      else
      {
        // 2. 準備中の「ドラッグ（移動）」完了時のスナップ処理
        SnapToGrid();
      }
    }

    // 右クリックの判定：盤面ならベンチへ退避、ベンチなら削除
    if (Mouse.current.rightButton.wasPressedThisFrame)
    {
      // ステップ11: UGUIボタン等の上での右クリックも同様にガード
      if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      {
        return;
      }

      Ray ray = mainCamera.ScreenPointToRay(mousePos);
      if (Physics.Raycast(ray, out RaycastHit hit))
      {
        if (hit.transform == transform)
        {
          // 課題3【所有者チェック】: 右クリックによるベンチ退避・削除も、
          // 自分の駒がプレイヤー所有（PlayerType.Player）の場合のみ許可する。
          if (!IsOwnedByLocalPlayer()) return;

          HandleRightClick();
        }
      }
    }
  }

  // 課題3【所有者チェック】: このコンポーネントが付与された駒がプレイヤー所有かどうかを判定する。
  // PieceData.Owner (PlayerType) が PlayerType.Player の場合のみ true を返す。
  // PieceDataが取得できない異常系では、安全側に倒して「操作不可」として扱う。
  // 課題【合成/融合の手動選択モード】: 選択モード中の左クリック単押し判定専用のハンドラ。
  // 既存のmouseDownPos/isDragging/DragThresholdDistanceの仕組みをそのまま流用し、
  // 「押した位置から一定距離以上動かさずに離した」場合のみ単押しとみなしてトグル選択する
  // （ドラッグ移動・右クリック操作は上位のUpdate()側で完全にスキップされているため、ここでは考慮不要）。
  // 選択モード中は敵駒も味方駒もクリックできてよく、実際にトグルされるかどうかは
  // UI_ToggleSelectionForPiece内の条件（isEnemy==false等、候補条件に合致するか）で自然に絞られるため、
  // ここでの追加の所有者チェックは不要。
  void HandleSelectionModeClick(Vector2 mousePos)
  {
    if (Mouse.current.leftButton.wasPressedThisFrame)
    {
      if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

      Ray ray = mainCamera.ScreenPointToRay(mousePos);
      if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
      {
        mouseDownPos = mousePos;
        mouseDownTime = Time.time;
        isDragging = true; // 「このフレームで自分が押された」の目印としてそのまま流用する
      }
    }

    if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
    {
      isDragging = false;

      float dist = Vector3.Distance(mouseDownPos, mousePos);
      if (dist < DragThresholdDistance && DebugGameManager.Instance != null)
      {
        DebugGameManager.Instance.UI_ToggleSelectionForPiece(GetComponent<PieceData>());
      }
    }
  }

  bool IsOwnedByLocalPlayer()
  {
    PieceData data = GetComponent<PieceData>();
    return data != null && data.Owner == PlayerType.Player;
  }

  // 右クリック時のロジック分岐
  void HandleRightClick()
  {
    bool inBench = DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(transform.position);

    // 課題4【デバッグ用「ベンチ駒の右クリック削除」機能の撤去】:
    // 以前はベンチ上の駒を右クリックするとその場でDestroy()される実装になっていたが、
    // これはデバッグ用の名残であり、正式な機能として不要なため削除した。
    // ベンチ上の駒を右クリックしても、現在は何も起こらない
    // （左クリックによる「選択（インスペクト）」「ドラッグ＆ドロップ移動」は従来通り正常に機能する）。
    if (inBench)
    {
      return;
    }

    // 盤面上にある場合 -> 空いているベンチ枠へ移動（この機能は維持する）
    if (DebugGameManager.Instance != null)
    {
      Vector3? emptyBenchPos = DebugGameManager.Instance.FindEmptyBenchPosition();
      if (emptyBenchPos.HasValue)
      {
        transform.position = emptyBenchPos.Value;
        Debug.Log($"【{gameObject.name}】 をベンチへ移動しました。");
      }
      else
      {
        Debug.LogWarning("⚠️ ベンチに空き枠がありません！");
      }
    }
  }

  void SnapToGrid()
  {
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm == null)
    {
      // 参照が取れない場合は安全のため元の位置へ戻す
      transform.position = new Vector3(startX, 0.25f, startZ);
      return;
    }

    // ステップ16: BoardParent基準のローカル座標へ変換してからグリッド判定を行う。
    // こうすることで、BoardParentの位置・回転・スケールを変更しても常に正しいマスへスナップする。
    bool inBench = gm.IsWorldPositionInBenchArea(transform.position);

    // 1. ベンチエリアへの配置
    if (inBench)
    {
      // 【ベンチの切り分け】盤面グリッドのZ座標ではなく、ベンチ専用のインデックス(0〜BenchSlotCount-1)でスナップする
      int benchIndex = Mathf.Clamp(gm.WorldToNearestBenchIndex(transform.position), 0, gm.BenchSlotCount - 1);
      Vector3 benchTarget = gm.BenchGridToWorldPosition(benchIndex, 0.25f);

      // ステップ8【不具合復旧】: ドロップ先のベンチ枠が既に埋まっている場合は、
      // 空いている別のベンチ枠を探して移動させる（見つからなければ元の位置へ戻す）
      if (IsCellOccupied(benchTarget))
      {
        Vector3? emptySlot = gm.FindEmptyBenchPosition();
        transform.position = emptySlot ?? new Vector3(startX, 0.25f, startZ);
      }
      else
      {
        transform.position = benchTarget;
      }
      return;
    }

    // 2. メイン盤面への配置
    Vector2Int gridIndex = gm.WorldToNearestGridIndex(transform.position);
    int gridX = Mathf.Clamp(gridIndex.x, 0, gm.BoardWidth - 1);
    int gridZ = Mathf.Clamp(gridIndex.y, 0, gm.BoardDepth - 1);

    bool isValidPlacement = true;

    // ステップ6/課題1: 自陣制限 — プレイヤー駒は手前 PlayerFrontRowDepth 行（既定: グリッドZ = 0〜1）のみ配置可能
    PieceData myData = GetComponent<PieceData>();
    if (myData != null && !myData.isEnemy)
    {
      bool isFrontRow = gridZ < gm.PlayerFrontRowDepth;
      if (!isFrontRow) isValidPlacement = false;
    }

    // ステップ8【不具合復旧】: セル重複防止 — ドロップ先に既に別の生存駒がいる場合は配置不可
    Vector3 boardTarget = gm.GridToWorldPosition(gridX, gridZ, 0.25f);
    if (isValidPlacement && IsCellOccupied(boardTarget))
    {
      isValidPlacement = false;
    }

    if (isValidPlacement)
    {
      transform.position = boardTarget;
    }
    else
    {
      // 条件を満たさない場合はドラッグ開始前の位置（ベンチだった場合はベンチ）へ安全に戻す
      transform.position = new Vector3(startX, 0.25f, startZ);
    }
  }

  // ステップ8/16: 指定セル（ワールド座標）に自分以外の生存駒が既にいるかどうかを判定。
  // 比較は常にワールド座標同士（実際の現在位置）で行うため、BoardParentの変更に関わらず正しく判定できる。
  bool IsCellOccupied(Vector3 targetCellWorld)
  {
    PieceData myData = GetComponent<PieceData>();

    float threshold = 0.6f;
    if (DebugGameManager.Instance != null) threshold *= DebugGameManager.Instance.WorldCellSize;

    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // ループ内のロジック（自分自身除外・currentHp<=0の生存フィルタ）は既存のまま一切変更しない。
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p == myData || p.currentHp <= 0) continue;

      float dist = Vector3.Distance(new Vector3(targetCellWorld.x, 0, targetCellWorld.z),
                                    new Vector3(p.transform.position.x, 0, p.transform.position.z));
      if (dist < threshold) return true;
    }
    return false;
  }
}
