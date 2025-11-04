using UnityEngine;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class SpecialUpgradeGiver : MonoBehaviour
{
    [Header("Rarity Settings")]
    [Tooltip("Chance (in %) to get a Mythic upgrade; remaining chance goes to Shadow.")]
    [Range(0, 100)]
    [SerializeField] private float mythicalChance = 50f;

    // Cached references
    private SpecialUpgradeUI specialUpgradePanel;
    private UpgradeManager upgradeManager;
    private GameManager gameManager;
    private Collider triggerCollider;

    // Internal data
    private UpgradeManager.GeneratedUpgrade generatedUpgrade;
    private PlayerStats triggeredPlayerStats;
    private bool initialized = false;

    private IEnumerator Start()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.enabled = false; // prevent premature activation

        // Wait a frame to ensure all singletons are initialized
        yield return new WaitForEndOfFrame();

        // Get cached instances or find them once if not assigned
        gameManager = GameManager.Instance;
    upgradeManager = UpgradeManager.Instance ?? Object.FindFirstObjectByType<UpgradeManager>();
    specialUpgradePanel = SpecialUpgradeUI.Instance ?? Object.FindFirstObjectByType<SpecialUpgradeUI>();

        if (gameManager == null || upgradeManager == null || specialUpgradePanel == null)
        {
            Debug.LogError(
                $"❌ [SpecialUpgradeGiver] Missing dependencies!\n" +
                $"GameManager: {(gameManager != null)}, " +
                $"UpgradeManager: {(upgradeManager != null)}, " +
                $"SpecialUpgradeUI: {(specialUpgradePanel != null)}",
                this
            );
            Destroy(gameObject);
            yield break;
        }

        initialized = true;
        triggerCollider.enabled = true;
        Debug.Log("✅ SpecialUpgradeGiver initialized successfully.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || !other.CompareTag("Player")) return;

        PlayerStats player = other.GetComponent<PlayerStats>();
        if (player == null) return;

        triggeredPlayerStats = player;
        triggerCollider.enabled = false;

        GenerateAndShowSpecialUpgrade();
    }

    private void GenerateAndShowSpecialUpgrade()
    {
        // Decide rarity
        bool isMythic = Random.Range(0f, 100f) < mythicalChance;
        Rarity rarityType = isMythic ? Rarity.Mythic : Rarity.Shadow;

        // Get tier info safely
        RarityTier rarityTier = upgradeManager.GetRarityTiers()
            .FirstOrDefault(t => t.rarity == rarityType);

        if (rarityTier == null)
        {
            Debug.LogError($"❌ No RarityTier found for {rarityType}.", this);
            Destroy(gameObject);
            return;
        }

        var upgrades = upgradeManager.GetAvailableUpgrades();
        if (upgrades == null || upgrades.Count == 0)
        {
            Debug.LogWarning("⚠️ No upgrades available to choose from.", this);
            Destroy(gameObject);
            return;
        }

        // Pick upgrade
        StatUpgradeData chosenData = upgrades[Random.Range(0, upgrades.Count)];
        float randomValue = Random.Range(chosenData.baseValueMin, chosenData.baseValueMax);
        float finalValue = randomValue * rarityTier.valueMultiplier;

        generatedUpgrade = new UpgradeManager.GeneratedUpgrade
        {
            BaseData = chosenData,
            Rarity = rarityTier,
            Value = finalValue
        };

        // Display panel
        specialUpgradePanel.Show(generatedUpgrade, this);
        gameManager.RequestPause();

        Debug.Log($"✨ Generated {rarityType} upgrade: {chosenData.statToUpgrade} +{finalValue}", this);
    }

    public void ApplyUpgradeAndDestroy()
    {
        if (generatedUpgrade == null || triggeredPlayerStats == null)
        {
            Debug.LogError("❌ Missing upgrade or player reference in ApplyUpgradeAndDestroy!", this);
            gameManager.RequestResume();
            Destroy(gameObject);
            return;
        }

        ApplyStatToPlayer(triggeredPlayerStats, generatedUpgrade.BaseData.statToUpgrade, generatedUpgrade.Value);
        Debug.Log($"✅ Applied {generatedUpgrade.Rarity.rarity} upgrade: {generatedUpgrade.BaseData.statToUpgrade} +{generatedUpgrade.Value}", this);

        gameManager.RequestResume();
        Destroy(gameObject);
    }

    private void ApplyStatToPlayer(PlayerStats player, StatType stat, float value)
    {
        switch (stat)
        {
            case StatType.MaxHP: player.IncreaseMaxHP(Mathf.RoundToInt(value)); break;
            case StatType.HPRegen: player.IncreaseHPRegen(value); break;
            case StatType.DamageMultiplier: player.IncreaseDamageMultiplier(value / 100f); break;
            case StatType.CritChance: player.IncreaseCritChance(value / 100f); break;
            case StatType.CritDamageMultiplier: player.IncreaseCritDamageMultiplier(value / 100f); break;
            case StatType.AttackSpeedMultiplier: player.IncreaseAttackSpeedMultiplier(value / 100f); break;
            case StatType.ProjectileCount: player.IncreaseProjectileCount(Mathf.RoundToInt(value)); break;
            case StatType.ProjectileSizeMultiplier: player.IncreaseProjectileSizeMultiplier(value / 100f); break;
            case StatType.ProjectileSpeedMultiplier: player.IncreaseProjectileSpeedMultiplier(value / 100f); break;
            case StatType.DurationMultiplier: player.IncreaseDurationMultiplier(value / 100f); break;
            case StatType.KnockbackMultiplier: player.IncreaseKnockbackMultiplier(value / 100f); break;
            case StatType.MovementSpeed: player.IncreaseMovementSpeed(value / 100f * player.movementSpeed); break;
            case StatType.Luck: player.IncreaseLuck(value); break;
            case StatType.PickupRange: player.IncreasePickupRange(value * player.pickupRange - player.pickupRange); break;
            case StatType.XPGainMultiplier: player.IncreaseXPGainMultiplier(value / 100f); break;
        }
    }
}
