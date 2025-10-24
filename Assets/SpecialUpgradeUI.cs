using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpecialUpgradeUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The background panel that will change color.")]
    [SerializeField] private Image panelBackground;
    [Tooltip("The image for the upgrade's icon.")]
    [SerializeField] private Image upgradeIcon;
    [Tooltip("The text that will show the stat name and its value.")]
    [SerializeField] private TextMeshProUGUI valueText; // Changed from upgradeNameText for clarity
    [Tooltip("The button to claim the upgrade.")]
    [SerializeField] private Button claimButton;

    private SpecialUpgradeGiver currentGiver;

    private void Awake()
    {
        claimButton.onClick.AddListener(OnClaimButtonPressed);
        gameObject.SetActive(false); // Start hidden
    }

    /// <summary>
    /// Shows the panel and fills it with the upgrade data.
    /// </summary>
    public void Show(UpgradeManager.GeneratedUpgrade upgrade, SpecialUpgradeGiver giver)
    {
        this.currentGiver = giver;

        // 1. Change panel color based on rarity
        panelBackground.color = upgrade.Rarity.backgroundColor;

        // 2. Set the icon
        upgradeIcon.sprite = upgrade.BaseData.icon;

        // 3. Set the text to the stat name and the final value
        // Example output: "Max HP\n+25.0"
        valueText.text = $"{upgrade.BaseData.upgradeName}\n+{upgrade.Value.ToString("F1")}";
        
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
        {
            currentGiver.ApplyUpgradeAndDestroy();
        }
        Hide();
    }
}