using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeChoiceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;

    private UpgradeManager.GeneratedUpgrade currentUpgrade;
    private UpgradeManager manager;

    public void Setup(UpgradeManager.GeneratedUpgrade upgrade, UpgradeManager manager)
    {
        this.currentUpgrade = upgrade;
        this.manager = manager;

        // Populate UI
        backgroundImage.color = upgrade.Rarity.backgroundColor;
        iconImage.sprite = upgrade.BaseData.icon;
        nameText.text = upgrade.BaseData.upgradeName;

        // Format the description to show the actual value
        string formattedValue = GetFormattedValue(upgrade.BaseData.statToUpgrade, upgrade.Value);
        descriptionText.text = $"{upgrade.BaseData.upgradeDescription}\n<color=green>+{formattedValue}</color>";
        
        // Set up the button
        selectButton.onClick.AddListener(OnSelect);
    }

    private string GetFormattedValue(StatType type, float value)
    {
        // These types are percentages
        if (type == StatType.DamageMultiplier || type == StatType.CritChance ||
            type == StatType.CritDamageMultiplier || type == StatType.AttackSpeedMultiplier ||
            type == StatType.ProjectileSizeMultiplier || type == StatType.ProjectileSpeedMultiplier ||
            type == StatType.DurationMultiplier || type == StatType.KnockbackMultiplier ||
            type == StatType.XPGainMultiplier)
        {
            return $"{value:F1}%";
        }
        
        // These types are integers
        if(type == StatType.MaxHP || type == StatType.ProjectileCount)
        {
            return Mathf.RoundToInt(value).ToString();
        }

        // All others are floats with 2 decimal places
        return $"{value:F2}";
    }

    private void OnSelect()
    {
        manager.ApplyUpgrade(currentUpgrade);
    }
}