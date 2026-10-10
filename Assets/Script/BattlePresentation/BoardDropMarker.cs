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

    private Transform visual;
    private Vector3 basePosition;
    private float bobPhase;

    public void Initialize(int dropId, EquipmentInstance item, Vector3 worldPosition)
    {
        DropId = dropId;
        Item = item;
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
