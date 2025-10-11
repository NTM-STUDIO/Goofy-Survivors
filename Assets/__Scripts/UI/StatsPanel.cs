using UnityEngine;
using TMPro; // Required for using TextMeshPro UI elements


public class StatsPanel : MonoBehaviour
{
    [Header("Data Source")]
    [Tooltip("Assign the Player's GameObject which has the PlayerStats script.")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Stat Text Fields")]
    [SerializeField] private TextMeshProUGUI maxHpText;
    [SerializeField] private TextMeshProUGUI hpRegenText;
    [SerializeField] private TextMeshProUGUI damageMultiplierText;
    [SerializeField] private TextMeshProUGUI critChanceText;
    [SerializeField] private TextMeshProUGUI critDamageMultiplierText;
    [SerializeField] private TextMeshProUGUI attackSpeedMultiplierText;
    [SerializeField] private TextMeshProUGUI projectileCountText;
    [SerializeField] private TextMeshProUGUI projectileSizeMultiplierText;
    [SerializeField] private TextMeshProUGUI projectileSpeedMultiplierText;
    [SerializeField] private TextMeshProUGUI durationMultiplierText;
    [SerializeField] private TextMeshProUGUI knockbackMultiplierText;
    [SerializeField] private TextMeshProUGUI movementSpeedText;
    [SerializeField] private TextMeshProUGUI luckText;
    [SerializeField] private TextMeshProUGUI pickupRangeText;
    [SerializeField] private TextMeshProUGUI xpGainMultiplierText;


    private void OnEnable()
    {
        playerStats = FindFirstObjectByType<PlayerStats>();

        // It's crucial to check if playerStats is assigned to prevent errors.
        if (playerStats == null)
        {
            Debug.LogError("PlayerStats is not assigned in the StatsPanel inspector!");
            return;
        }

        // Subscribe to the health change event to update HP in real-time.
        playerStats.OnHealthChanged += UpdateHealth;

        // Perform an initial update of all stats.
        UpdateAllStatDisplays();
    }

    private void OnDisable()
    {
        // Unsubscribe from the event when the UI is disabled or destroyed to prevent memory leaks.
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHealth;
        }
    }

    /// <summary>
    /// Updates all stat text fields. Call this method when stats are changed (e.g., on level up).
    /// </summary>
    public void UpdateAllStatDisplays()
    {
        if (playerStats == null) return;

        // Update health first
        UpdateHealth(playerStats.CurrentHp, playerStats.maxHp);

        // Update the rest of the stats
        hpRegenText.text = $"HP Regen: {playerStats.hpRegen:F2}/s";
        damageMultiplierText.text = $"Damage: {playerStats.damageMultiplier * 100:F0}%";
        critChanceText.text = $"Crit Chance: {playerStats.critChance:P1}"; // P1 formats as a percentage with 1 decimal
        critDamageMultiplierText.text = $"Crit Damage: {playerStats.critDamageMultiplier * 100:F0}%";
        attackSpeedMultiplierText.text = $"Attack Speed: {playerStats.attackSpeedMultiplier * 100:F0}%";
        projectileCountText.text = $"Projectiles: {playerStats.projectileCount}";
        projectileSizeMultiplierText.text = $"Area/Size: {playerStats.projectileSizeMultiplier * 100:F0}%";
        projectileSpeedMultiplierText.text = $"Proj. Speed: {playerStats.projectileSpeedMultiplier * 100:F0}%";
        durationMultiplierText.text = $"Duration: {playerStats.durationMultiplier * 100:F0}%";
        knockbackMultiplierText.text = $"Knockback: {playerStats.knockbackMultiplier * 100:F0}%";
        movementSpeedText.text = $"Move Speed: {playerStats.movementSpeed}";
        luckText.text = $"Luck: {playerStats.luck}";
        pickupRangeText.text = $"Pickup Range: {playerStats.pickupRange}";
        xpGainMultiplierText.text = $"XP Gain: {playerStats.xpGainMultiplier:P0}";
    }

    /// <summary>
    /// Callback method to update the health display.
    /// This is automatically called by the OnHealthChanged event from PlayerStats.
    /// </summary>
    /// <param name="current">Current HP</param>
    /// <param name="max">Maximum HP</param>
    private void UpdateHealth(int current, int max)
    {
        maxHpText.text = $"Health: {current} / {max}";
    }
}