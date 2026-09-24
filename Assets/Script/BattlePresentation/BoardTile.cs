using UnityEngine;

public enum HighlightType
{
    None,
    Movable,
    Attackable
}

[RequireComponent(typeof(Renderer))]
public class BoardTile : MonoBehaviour
{
    [Header("ハイライト色")]
    public Color movableColor = new Color(0.3f, 0.5f, 1f);
    public Color attackableColor = new Color(1f, 0.3f, 0.3f);

    private Renderer ren;
    private Material defaultMaterial;

    public void Initialize(Material baseMaterial)
    {
        ren = GetComponent<Renderer>();
        defaultMaterial = baseMaterial;
        ren.material = baseMaterial;
    }

    public void SetHighlight(HighlightType type)
    {
        switch (type)
        {
            case HighlightType.None:
                ren.material = defaultMaterial;
                break;
            case HighlightType.Movable:
                ren.material.color = movableColor;
                break;
            case HighlightType.Attackable:
                ren.material.color = attackableColor;
                break;
        }
    }
}
