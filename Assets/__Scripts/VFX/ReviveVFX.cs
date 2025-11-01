using UnityEngine;
using System.Collections;

public class ReviveVFX : MonoBehaviour
{
    public static void Spawn(
        Vector3 worldPosition,
        Sprite sprite,
        Color color,
        float duration = 0.8f,
        float startScale = 0.8f,
        float endScale = 1.3f,
        float yOffset = 2.0f
    )
    {
        if (sprite == null) return;
        GameObject go = new GameObject("ReviveVFX");
        go.transform.position = worldPosition + Vector3.up * yOffset;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 9999; // ensure on top
        go.transform.localScale = Vector3.one * startScale;

        var vfx = go.AddComponent<ReviveVFX>();
        vfx._renderer = sr;
        vfx._duration = Mathf.Max(0.05f, duration);
        vfx._startScale = startScale;
        vfx._endScale = endScale;
        vfx._startColor = color;
        vfx._endColor = new Color(color.r, color.g, color.b, 0f);
        vfx._faceCamera = true;
    }

    private SpriteRenderer _renderer;
    private float _duration;
    private float _startScale;
    private float _endScale;
    private Color _startColor;
    private Color _endColor;
    private bool _faceCamera;

    private void OnEnable()
    {
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        float t = 0f;
        while (t < _duration)
        {
            t += Time.unscaledDeltaTime; // unaffected by pause/game over
            float u = Mathf.Clamp01(t / _duration);

            // Scale
            float s = Mathf.Lerp(_startScale, _endScale, EaseOutCubic(u));
            transform.localScale = Vector3.one * s;

            // Fade
            if (_renderer != null)
            {
                _renderer.color = Color.LerpUnclamped(_startColor, _endColor, u);
            }

            // Face camera
            if (_faceCamera && Camera.main != null)
            {
                var fwd = Camera.main.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private static float EaseOutCubic(float x)
    {
        return 1 - Mathf.Pow(1 - x, 3);
    }
}
