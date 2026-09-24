using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

// ステップ22【不具合修正】: 以前はUIバー分だけCamera.rect（ビューポート）を狭めて
// 3D描画エリアとUIエリアを分離していたが、現在のUIはScreen Space - Overlay Canvasで
// 画面全体に独立して描画される方式へ移行済みのため、この仕組みは不要かつ実害
// （画面下部に黒い隙間ができる）があった。ビューポート操作は完全に撤去し、
// カメラは常に画面全体 (X:0, Y:0, W:1, H:1) を描画するようにする。
//
// 【今回改修・重要】: このプロジェクトはPieceDraggable.cs / DebugGameManager.cs など、
// マウス入力を全て新Input System（Mouse.current）で扱う方式に統一されている。
// 旧来のUnityEngine.Input（レガシーInput Manager）は「Active Input Handling」設定が
// 「Input System Package (New)」のみの場合、呼び出した瞬間に例外を投げて動作しない。
// そのため、このスクリプトも他のスクリプトと同じくMouse.current経由で入力を取得するよう統一した。
//
// 【今回改修】:
//   ・右クリックドラッグでの盤面中心を軸にした軌道回転（既存）
//   ・マウスホイールでのズームイン/アウト（既存）
//   ・中クリック（ホイール押し込み）ドラッグでのパン移動（新規・盤面範囲内にClamp）
//   ・自陣（プレイヤー側）が画面手前に来るような固定の初期視点（新規）
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
  // 画面全体を描画するための固定値。将来的にレターボックス演出等で意図的に
  // ビューポートを変更したい場合以外は触らない。
  private static readonly Rect FullScreenRect = new Rect(0f, 0f, 1f, 1f);

  [Header("注視点（Target）")]
  [Tooltip("カメラが常に注視し、軌道回転の中心となるTransform。未設定の場合はワールド原点(0,0,0)を注視点として扱う（盤面中心合わせ後のデフォルトはここで正しく機能する）")]
  [SerializeField] private Transform target;

  [Header("課題3: 初期視点（自陣を手前に）")]
  [Tooltip("ONの場合、シーンに配置されたカメラの現在位置は無視し、下記のInitial Yaw/Pitch/Distanceを使って\n" +
           "「プレイヤー自身の駒が画面手前（下側）、敵の駒が画面奥（上側）」に見える視点で開始する。\n" +
           "OFFの場合は従来通り、シーン上のカメラの現在位置・向きから初期のyaw/pitch/distanceを逆算する。")]
  [SerializeField] private bool useFixedInitialView = true;
  [Tooltip("初期の水平回転角（Yaw）。盤面のZ座標は「プレイヤー側が負の値、敵側が正の値」になるよう設計されているため、\nYaw=0がプレイヤー側を画面手前にする基準値になる")]
  [SerializeField] private float initialYaw = 0f;
  [Tooltip("初期の見下ろし角（Pitch）")]
  [SerializeField] private float initialPitch = 50f;
  [Tooltip("初期のtargetからの距離。盤面・ベンチ全体が画面に収まる程度の値を推奨")]
  [SerializeField] private float initialDistance = 11f;

  [Header("右クリックドラッグ回転")]
  [Tooltip("回転速度（度/マウス移動量）。大きいほど少ないドラッグ量で大きく回転する")]
  [SerializeField] private float rotateSpeed = 0.25f;
  [Tooltip("見下ろし角度（Pitch）の下限。0=水平、90=真上から見下ろし。小さすぎる値は盤面が横から潰れて見えるようになる")]
  [SerializeField] private float minPitch = 15f;
  [Tooltip("見下ろし角度（Pitch）の上限。90に近いほど真上からの視点に近づく")]
  [SerializeField] private float maxPitch = 80f;
  [Tooltip("回転操作のなめらかさ（秒）。値が大きいほどゆっくり追従する。0にすると入力に対して即座に反応する")]
  [SerializeField] private float rotateSmoothTime = 0.06f;

  [Header("マウスホイールズーム")]
  [Tooltip("ズーム速度。ホイール1ノッチあたりに距離を変化させる量。Mouse.current.scrollの値の大きさは環境依存のため、実機で違和感があればこの値をInspectorで調整してください")]
  [SerializeField] private float zoomSpeed = 0.02f;
  [Tooltip("targetに最も近づける距離（これ以上は近づけない）")]
  [SerializeField] private float minDistance = 3f;
  [Tooltip("targetから最も離れられる距離（これ以上は離れられない）")]
  [SerializeField] private float maxDistance = 20f;
  [Tooltip("ズーム操作のなめらかさ（秒）。値が大きいほどゆっくり追従する。0にすると入力に対して即座に反応する")]
  [SerializeField] private float zoomSmoothTime = 0.08f;

  [Header("課題2: 中クリックドラッグ パン移動")]
  [Tooltip("パン移動の速度。大きいほど少ないドラッグ量で大きく移動する")]
  [SerializeField] private float panSpeed = 0.01f;
  [Tooltip("パン操作のなめらかさ（秒）")]
  [SerializeField] private float panSmoothTime = 0.05f;
  [Tooltip("ONの場合、DebugGameManagerの盤面サイズ(BoardWidth/BoardDepth)からパン移動可能範囲を自動算出する。\nOFFの場合は下のPan Limit X/Zを直接使用する")]
  [SerializeField] private bool autoClampToBoard = true;
  [Tooltip("盤面中心からX方向へパン移動できる最大距離（autoClampToBoard=OFFの時のみ使用）")]
  [SerializeField] private float panLimitX = 4f;
  [Tooltip("盤面中心からZ方向へパン移動できる最大距離（autoClampToBoard=OFFの時のみ使用）")]
  [SerializeField] private float panLimitZ = 5f;
  [Tooltip("autoClampToBoard=ONの時、算出した盤面範囲にさらに余裕（マス単位）を持たせる")]
  [SerializeField] private float panLimitMargin = 1.5f;

  [Tooltip("UI（ボタン等）の上でドラッグ操作（右クリック/中クリック）を開始した場合、カメラ操作を無視するかどうか")]
  [SerializeField] private bool ignoreDragStartedOverUI = true;

  private Camera cam;

  // 目標値（入力によって即座に更新される、クランプ済みの「あるべき」角度・距離・パンオフセット）
  private float targetYaw;
  private float targetPitch;
  private float targetDistance;
  private Vector3 targetPanOffset = Vector3.zero; // X,Zのみ使用

  // 実際に毎フレーム描画へ反映される、スムーズ追従後の現在値
  private float currentYaw;
  private float currentPitch;
  private float currentDistance;
  private Vector3 currentPanOffset = Vector3.zero;

  // SmoothDamp系関数が内部で使用する速度キャッシュ
  private float yawVelocity;
  private float pitchVelocity;
  private float distanceVelocity;
  private Vector3 panVelocity;

  private Vector2 lastMousePosition;
  private bool isRotating = false;
  private bool isPanning = false;

  void Start()
  {
    cam = GetComponent<Camera>();
    if (cam == null) cam = Camera.main;

    EnsureFullScreenViewport();
    InitializeOrbit();
  }

  void InitializeOrbit()
  {
    if (useFixedInitialView)
    {
      // 課題3【視点の反転】: シーン上のカメラの現在位置は無視し、常に
      // 「プレイヤー側の駒が画面手前（下側）、敵側の駒が画面奥（上側）」に見える固定視点から開始する。
      targetYaw = currentYaw = initialYaw;
      targetPitch = currentPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
      targetDistance = currentDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
    }
    else
    {
      // 従来通り: 現在シーンに配置されているカメラの位置・向きから初期のyaw/pitch/distanceを逆算する。
      InitializeOrbitFromCurrentTransform();
    }

    UpdatePanLimitsFromBoard();
    targetPanOffset = Vector3.zero;
    currentPanOffset = Vector3.zero;

    ApplyCameraTransform(currentYaw, currentPitch, currentDistance, currentPanOffset);
  }

  // 現在シーンに配置されているカメラの位置・向きから、初期のyaw/pitch/distanceを逆算する（useFixedInitialView=OFF時のみ使用）。
  void InitializeOrbitFromCurrentTransform()
  {
    Vector3 targetPos = GetFocusPointBase();
    Vector3 offset = transform.position - targetPos;

    float distance = offset.magnitude;
    if (distance < 0.001f) distance = (minDistance + maxDistance) * 0.5f; // カメラがtargetと同じ位置にある異常値への保険

    float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
    float pitch = Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f)) * Mathf.Rad2Deg;

    targetDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    targetYaw = currentYaw = yaw;
    targetPitch = currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
  }

  // targetが未設定の場合はワールド原点（盤面中心合わせ後のデフォルトの盤面中心）を基準点とする
  Vector3 GetFocusPointBase()
  {
    return target != null ? target.position : Vector3.zero;
  }

  // 課題2: パンオフセットを加味した、実際にカメラが注視する現在の焦点座標
  Vector3 GetFocusPoint()
  {
    return GetFocusPointBase() + currentPanOffset;
  }

  // 課題2【範囲制限】: DebugGameManagerの盤面サイズ(BoardWidth/BoardDepth)からパン移動可能範囲を自動算出する。
  // DebugGameManagerが見つからない場合は、Inspectorで設定したpanLimitX/Zをそのまま使う。
  void UpdatePanLimitsFromBoard()
  {
    if (!autoClampToBoard) return;
    if (DebugGameManager.Instance == null) return;

    // 盤面（ベンチ含む）がおおよそ画面外へ完全に出てしまわない範囲を、盤面サイズの半分＋余裕分として算出する
    panLimitX = DebugGameManager.Instance.BoardWidth / 2f + panLimitMargin;
    panLimitZ = DebugGameManager.Instance.BoardDepth / 2f + panLimitMargin;
  }

  void Update()
  {
    if (cam == null) cam = Camera.main;
    if (cam == null) return;

    // 何らかの理由でrectが書き換えられていた場合に備え、毎フレーム画面全体へ戻す
    EnsureFullScreenViewport();

    if (Mouse.current == null) return;

    HandleRotationInput();
    HandlePanInput();
    HandleZoomInput();
  }

  void LateUpdate()
  {
    if (cam == null) return;

    // なめらかな追従: 目標値へ現在値を滑らかに近づける。
    // 各SmoothTimeが0の場合はSmoothDamp系がほぼ即座に追従するため、
    // 「なめらかさ無し（入力に対してダイレクト）」の挙動にもそのまま対応できる。
    currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotateSmoothTime);
    currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, rotateSmoothTime);
    currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);
    currentPanOffset = Vector3.SmoothDamp(currentPanOffset, targetPanOffset, ref panVelocity, panSmoothTime);

    ApplyCameraTransform(currentYaw, currentPitch, currentDistance, currentPanOffset);
  }

  // yaw(水平角)・pitch(見下ろし角)・distance(焦点からの距離)の球面座標から、
  // カメラの実際のワールド位置・向きを求めて適用する。焦点自体はGetFocusPointBase()+panOffsetで決まる。
  void ApplyCameraTransform(float yaw, float pitch, float distance, Vector3 panOffset)
  {
    Vector3 focusPoint = GetFocusPointBase() + panOffset;

    Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
    Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

    transform.position = focusPoint + offset;
    transform.rotation = rotation;
  }

  void HandleRotationInput()
  {
    // 右クリック押下開始
    if (Mouse.current.rightButton.wasPressedThisFrame)
    {
      if (ignoreDragStartedOverUI && IsPointerOverUI())
      {
        isRotating = false;
      }
      else
      {
        isRotating = true;
        lastMousePosition = Mouse.current.position.ReadValue();
      }
    }

    if (Mouse.current.rightButton.wasReleasedThisFrame)
    {
      isRotating = false;
    }

    if (!isRotating || !Mouse.current.rightButton.isPressed) return;

    Vector2 currentMousePosition = Mouse.current.position.ReadValue();
    Vector2 delta = currentMousePosition - lastMousePosition;
    lastMousePosition = currentMousePosition;

    // 右クリックドラッグ: X移動→水平回転(yaw)、Y移動→上下の見下ろし角(pitch)。
    // Y方向はマウスを上に動かした時にカメラが見上げる方向へ回転するよう符号を反転させている。
    targetYaw += delta.x * rotateSpeed;
    targetPitch -= delta.y * rotateSpeed;
    targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
  }

  // 課題2: 中クリック（ホイール押し込み）ドラッグによるパン移動
  void HandlePanInput()
  {
    if (Mouse.current.middleButton.wasPressedThisFrame)
    {
      if (ignoreDragStartedOverUI && IsPointerOverUI())
      {
        isPanning = false;
      }
      else
      {
        isPanning = true;
        lastMousePosition = Mouse.current.position.ReadValue();
      }
    }

    if (Mouse.current.middleButton.wasReleasedThisFrame)
    {
      isPanning = false;
    }

    if (!isPanning || !Mouse.current.middleButton.isPressed) return;

    Vector2 currentMousePosition = Mouse.current.position.ReadValue();
    Vector2 delta = currentMousePosition - lastMousePosition;
    lastMousePosition = currentMousePosition;
    if (delta.sqrMagnitude < 0.0001f) return;

    // 「盤面に沿って」移動させるため、カメラの向きを水平面（XZ平面）に投影したright/forwardベクトルを使う。
    // これにより、どの回転角度（yaw/pitch）から見ていても、ドラッグ方向と画面上の移動方向が直感的に一致する。
    Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

    // ドラッグした方向へ画面上の景色がついてくる（＝カメラの焦点はドラッグと逆方向へ動く）感覚になるよう符号を設定
    Vector3 panDelta = (-flatRight * delta.x - flatForward * delta.y) * panSpeed;

    Vector3 newOffset = targetPanOffset + panDelta;

    // 課題2【範囲制限】: 盤面が画面外に完全に飛び出さないよう、X/Zそれぞれ独立にClampする
    newOffset.x = Mathf.Clamp(newOffset.x, -panLimitX, panLimitX);
    newOffset.z = Mathf.Clamp(newOffset.z, -panLimitZ, panLimitZ);
    newOffset.y = 0f;

    targetPanOffset = newOffset;
  }

  void HandleZoomInput()
  {
    Vector2 scroll = Mouse.current.scroll.ReadValue();
    if (Mathf.Approximately(scroll.y, 0f)) return;

    // スクロールアップ(正の値)でズームイン=距離を縮める、スクロールダウンでズームアウト=距離を伸ばす。
    // Mouse.current.scroll の1ノッチあたりの絶対値は環境によって異なるため、強さの調整はzoomSpeed（Inspector）側で行う想定。
    targetDistance -= scroll.y * zoomSpeed;
    targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
  }

  bool IsPointerOverUI()
  {
    return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
  }

  void EnsureFullScreenViewport()
  {
    if (cam == null) return;

    if (cam.rect != FullScreenRect)
    {
      cam.rect = FullScreenRect;
    }
  }
}
