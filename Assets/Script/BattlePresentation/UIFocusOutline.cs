using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// キーボードで選択されているUI（ボタンなど）を、黄色い縁取りで示す汎用コンポーネント。
// 選択されている間だけOutlineを有効にする。Outlineはこのコンポーネントが自動で追加する（無効状態で）。
public class UIFocusOutline : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private static readonly Color OutlineColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Vector2 OutlineDistance = new Vector2(4f, -4f);

    private Outline outline;

    void Awake()
    {
        EnsureOutline();
    }

    public void OnSelect(BaseEventData eventData)
    {
        EnsureOutline();
        outline.enabled = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        EnsureOutline();
        outline.enabled = false;
    }

    void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }

    private void EnsureOutline()
    {
        if (outline != null) return;

        outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();

        outline.effectColor = OutlineColor;
        outline.effectDistance = OutlineDistance;
        outline.enabled = false;
    }
}
