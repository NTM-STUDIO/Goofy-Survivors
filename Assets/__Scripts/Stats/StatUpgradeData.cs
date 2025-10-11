using UnityEngine;

[CreateAssetMenu(fileName = "New Stat Upgrade", menuName = "Upgrades/Stat Upgrade")]
public class StatUpgradeData : ScriptableObject
{
    [Header("Info")]
    public StatType statToUpgrade;
    public string upgradeName;
    [TextArea]
    public string upgradeDescription;
    public Sprite icon;

    [Header("Value Ranges")]
    [Tooltip("The minimum base value for this upgrade.")]
    public float baseValueMin;
    [Tooltip("The maximum base value for this upgrade.")]
    public float baseValueMax;

}