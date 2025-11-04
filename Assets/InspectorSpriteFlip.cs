using UnityEngine;

/// <summary>
/// Simple, inspector-friendly flipping utility.
/// Lets you flip a SpriteRenderer (and optionally all child SpriteRenderers)
/// by toggling booleans in the Inspector. Useful for quick authoring without
/// writing custom animation logic.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class InspectorSpriteFlip : MonoBehaviour
{
    [Header("Flip Settings")]
    public bool flipX;
    public bool flipY;

    [Tooltip("Also apply flipX/flipY to all child SpriteRenderers.")]
    public bool includeChildren = false;

    [Tooltip("Continuously re-apply in Edit/Play mode every frame. Disable to avoid fighting runtime scripts.")]
    public bool applyEveryFrame = false;

    private void OnEnable()
    {
        ApplyFlip();
    }

    private void OnValidate()
    {
        ApplyFlip();
    }

    private void Update()
    {
        if (applyEveryFrame)
        {
            ApplyFlip();
        }
    }

    public void ApplyFlip()
    {
        if (includeChildren)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                sr.flipX = flipX;
                sr.flipY = flipY;
            }
        }
        else
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = flipX;
                sr.flipY = flipY;
            }
        }
    }
}
