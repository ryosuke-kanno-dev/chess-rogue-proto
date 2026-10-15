using UnityEngine;

// 盤面に落ちているドロップの見た目。Prefabは使わず、実行時にSphereを生成する。
// BattleVisualSync.SyncDropsが、BoardStateのドロップ状態に合わせて生成・破棄する。
public class BoardDropMarker : MonoBehaviour
{
    private const float VisualScale = 0.35f;
    private const float BobAmplitude = 0.08f;
    private const float BobSpeed = 2.5f;
    private const float RotateDegreesPerSecond = 60f;

    public int DropId { get; private set; }
    public EquipmentInstance Item { get; private set; }
    public EquipmentInstance BoostedItem { get; private set; }

    private Transform visual;
    private bool showBoostDisc; // 鑑定Lv1以上のときだけ円盤を表示する
    private GameObject disc; // 拾得後のレアリティの色の円盤（球は上下に浮遊するが、円盤はマスの上に固定）
    private Vector3 basePosition;
    private float bobPhase;

    public void Initialize(int dropId, EquipmentInstance item, EquipmentInstance boostedItem, bool showBoostDisc, Vector3 worldPosition)
    {
        DropId = dropId;
        Item = item;
        BoostedItem = boostedItem;
        this.showBoostDisc = showBoostDisc;
        basePosition = worldPosition;
        transform.position = worldPosition;
        bobPhase = dropId * 0.7f; // 複数のドロップが同じ位相で揺れないようにずらす

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "DropVisual";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * VisualScale;
        visual = sphere.transform;

        // Raycastを妨げないよう（駒・タイルのクリック判定を横取りしない）Colliderは即座に除去する
        var collider = sphere.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = GetRarityColor(item != null ? item.rarity : EquipmentRarity.Common);

        CreateBoostDisc(worldPosition);
    }

    // 拾得ボーナス（+1版）がある場合だけ、球の下に、拾得後のレアリティの色の薄い円盤を置く
    private void CreateBoostDisc(Vector3 spherePosition)
    {
        if (BoostedItem == null || !showBoostDisc) return;

        disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "DropBoostDisc";
        // Cylinderの標準は直径1・高さ2。直径0.7・高さ0.03にする
        disc.transform.localScale = new Vector3(0.7f, 0.015f, 0.7f);
        // 球の中心はマスの上面から0.45上。その真下、マスの上面のすぐ上に置く
        disc.transform.position = spherePosition + Vector3.down * 0.43f;

        // Raycastを妨げないよう、Colliderは付けない
        var discCollider = disc.GetComponent<Collider>();
        if (discCollider != null) Destroy(discCollider);

        var discRenderer = disc.GetComponent<Renderer>();
        if (discRenderer != null)
            discRenderer.material.color = GetRarityColor(BoostedItem.rarity);
    }

    void OnDestroy()
    {
        if (disc != null) Destroy(disc);
    }

    void Update()
    {
        float bob = Mathf.Sin(Time.time * BobSpeed + bobPhase) * BobAmplitude;
        transform.position = basePosition + Vector3.up * bob;

        if (visual != null)
            visual.Rotate(0f, RotateDegreesPerSecond * Time.deltaTime, 0f, Space.World);
    }

    // 盤面マーカーと戦利品表示（BattleResultPanelUI）で同じ色を使うための共通メソッド
    public static Color GetRarityColor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Rare:      return new Color(0.3f, 0.6f, 1f);
            case EquipmentRarity.Epic:      return new Color(0.7f, 0.3f, 0.9f);
            case EquipmentRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
            case EquipmentRarity.Common:
            default:                        return new Color(0.8f, 0.8f, 0.8f);
        }
    }
}
