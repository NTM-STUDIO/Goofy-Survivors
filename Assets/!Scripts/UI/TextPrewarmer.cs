using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Workaround for UI Toolkit/TextCore jobs trying to touch FontAsset from worker threads during scene load.
// Pre-initialize frequently used TMP font assets on the main thread.
[DefaultExecutionOrder(-10000)] // Run very early
public class TextPrewarmer : MonoBehaviour
{
    [Tooltip("Font assets to prewarm on Awake to avoid worker-thread initialization.")]
    public List<TMP_FontAsset> fontAssets = new();

    [Tooltip("Automatically collect fonts from all TMP_Text components in the scene and TMP Settings.")]
    public bool autoCollectFromScene = true;

    private readonly HashSet<TMP_FontAsset> _collected = new();

    private void Awake()
    {
        if (autoCollectFromScene)
        {
            AutoCollectFonts();
        }

        // Merge serialized list
        foreach (var fa in fontAssets)
        {
            if (fa != null) _collected.Add(fa);
        }

        // Access a benign property to force the asset to initialize on the main thread
        foreach (var fa in _collected)
        {
            try
            {
                // Force internal initialization by accessing faceInfo and material
                var _ = fa.faceInfo; // triggers ReadFontAssetDefinition() inside TMP
                if (fa.material != null)
                {
                    var __ = fa.material.shader;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TextPrewarmer] Failed to prewarm font '{fa?.name}': {e.Message}");
            }
        }
    }

    private void AutoCollectFonts()
    {
        // Collect from scene TMP_Text components (includes TextMeshPro and TextMeshProUGUI)
        // includeInactive = true to catch disabled UI
#if UNITY_2023_2_OR_NEWER
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
#else
        var texts = Object.FindObjectsOfType<TMP_Text>(true);
#endif
        foreach (var t in texts)
        {
            if (t != null && t.font != null)
            {
                _collected.Add(t.font);
            }
        }

        // Collect from TMP Settings (default and fallbacks) if available
        // Some TMP versions expose these as static properties
        var defaultFA = TMP_Settings.defaultFontAsset;
        if (defaultFA != null) _collected.Add(defaultFA);

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null)
        {
            foreach (var fa in fallbacks)
            {
                if (fa != null) _collected.Add(fa);
            }
        }
    }
}
