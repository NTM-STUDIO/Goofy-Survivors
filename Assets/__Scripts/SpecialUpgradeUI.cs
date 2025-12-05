using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpecialUpgradeUI : MonoBehaviour
{
    public static SpecialUpgradeUI Instance { get; private set; }

    [Header("UI Elements")]
    [Tooltip("The background panel that will change color.")]
    [SerializeField] private Image panelBackground;

    [Tooltip("The image for the upgrade's icon.")]
    [SerializeField] private Image upgradeIcon;

    [Tooltip("The text that will show the stat name and its value.")]
    [SerializeField] private TextMeshProUGUI valueText;

    [Tooltip("The button to claim the upgrade.")]
    [SerializeField] private Button claimButton;

    private SpecialUpgradeGiver currentGiver;

    private void Awake()
    {
        // Singleton setup (scene-local)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate SpecialUpgradeUI found in scene. Destroying {gameObject.name}.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        claimButton.onClick.AddListener(OnClaimButtonPressed);
        gameObject.SetActive(false); // Start hidden
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Shows the panel and fills it with the upgrade data.
    /// </summary>
    public void Show(UpgradeManager.GeneratedUpgrade upgrade, SpecialUpgradeGiver giver)
    {
        if (upgrade == null || giver == null)
        {
            Debug.LogError("SpecialUpgradeUI.Show called with invalid parameters.", this);
            return;
        }

        currentGiver = giver;

        // 1. Change panel color based on rarity
        if (panelBackground != null)
            panelBackground.color = upgrade.Rarity.backgroundColor;

        // 2. Set the icon
        if (upgradeIcon != null)
            upgradeIcon.sprite = upgrade.BaseData.icon;

        // 3. Set the text (e.g., "Max HP\n+25.0")
        if (valueText != null)
            valueText.text = $"{upgrade.BaseData.upgradeName}\n+{upgrade.Value:F1}";

        // Show the panel
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
        currentGiver = null;
    }

    private void OnClaimButtonPressed()
    {
        if (currentGiver != null)
            currentGiver.ApplyUpgradeAndDestroy();

        Hide();
    }
}
