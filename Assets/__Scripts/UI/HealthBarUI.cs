using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private PlayerStats playerStats;

    [Header("UI Elements (choose one)")]
    [SerializeField] private Slider slider;          // Use for standard UI Slider
    [SerializeField] private Image fillImage;        // Or use an Image with Fill method
    [SerializeField] private Gradient colorGradient; // Optional: color by health percent

    [Header("Animation")]
    [SerializeField] private bool smooth = true;
    [SerializeField] private float lerpSpeed = 8f;

    private float targetFill01 = 1f;

    private void Awake()
    {
        if (playerStats == null)
        {
            // Prefer the LOCAL player's PlayerStats when using Netcode
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
            {
                playerStats = nm.LocalClient.PlayerObject.GetComponent<PlayerStats>();
            }
            // Fallback for single-player/editor
            if (playerStats == null)
            {
#if UNITY_2023_1_OR_NEWER
                playerStats = Object.FindFirstObjectByType<PlayerStats>();
#else
                playerStats = FindObjectOfType<PlayerStats>();
#endif
            }
        }
    }

    private void OnEnable()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged += HandleHealthChanged;
            playerStats.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= HandleHealthChanged;
            playerStats.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        // Initialize once when scene starts in case the initial event fired before we subscribed
        if (playerStats != null)
        {
            UpdateBar(playerStats.CurrentHp, playerStats.maxHp, immediate: true);
        }
    }

    private void Update()
    {
        if (!smooth) return;
        if (fillImage != null)
        {
            float current = fillImage.fillAmount;
            float next = Mathf.MoveTowards(current, targetFill01, lerpSpeed * Time.deltaTime);
            fillImage.fillAmount = next;
            if (colorGradient != null)
            {
                fillImage.color = colorGradient.Evaluate(next);
            }
        }
        else if (slider != null)
        {
            float current = slider.value;
            float target = targetFill01 * (slider.maxValue > 0 ? slider.maxValue : 1f);
            float next = Mathf.MoveTowards(current, target, lerpSpeed * Time.deltaTime * (slider.maxValue > 0 ? slider.maxValue : 1f));
            slider.value = next;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        UpdateBar(current, max, immediate: !smooth);
    }

    private void HandleDeath()
    {
        // Optionally hide or empty the bar on death
        UpdateBar(0, Mathf.Max(1, playerStats != null ? playerStats.maxHp : 1), immediate: true);
        // gameObject.SetActive(false); // uncomment if you prefer to hide it
    }

    private void UpdateBar(int current, int max, bool immediate)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        float pct = (float)current / max;
        targetFill01 = pct;

        if (slider != null)
        {
            slider.maxValue = max;
            if (immediate)
            {
                slider.value = current;
            }
        }

        if (fillImage != null)
        {
            if (immediate)
            {
                fillImage.fillAmount = pct;
            }
            if (colorGradient != null)
            {
                Color c = colorGradient.Evaluate(pct);
                fillImage.color = c;
            }
        }
    }
}
