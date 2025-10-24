using UnityEngine;
using System.Linq;
using System.Collections; // Required for Coroutines

public class SpecialUpgradeGiver : MonoBehaviour
{
    [Header("Rarity Settings")]
    [Tooltip("The chance (out of 100) to get a Mythic upgrade. The rest is the chance for Shadow.")]
    [Range(0, 100)]
    [SerializeField] private float mythicalChance = 50f;

    // Internal References
    private SpecialUpgradeUI specialUpgradePanel;
    private UpgradeManager upgradeManager;
    private GameManager gameManager;

    private UpgradeManager.GeneratedUpgrade generatedUpgrade;
    private PlayerStats triggeredPlayerStats; 
    
    // --- THIS IS THE KEY CHANGE ---
    // We now use a coroutine to ensure all other managers are ready before we try to find them.
    IEnumerator Start()
    {
        // 1. Disable the collider immediately to prevent the player from triggering it too early.
        GetComponent<Collider>().enabled = false;

        // 2. Wait for the end of the first frame.
        // By this point, all Awake() and Start() methods on other scripts have run.
        yield return new WaitForEndOfFrame();

        // 3. Now, it is safe to find our dependencies.
        gameManager = GameManager.Instance;
        upgradeManager = FindObjectOfType<UpgradeManager>();
        specialUpgradePanel = FindObjectOfType<SpecialUpgradeUI>();

        // 4. Critical check to ensure the script can function.
        if (gameManager == null || upgradeManager == null || specialUpgradePanel == null)
        {
            Debug.LogError("FATAL ERROR on SpecialUpgradeGiver: Could not find GameManager, UpgradeManager, or SpecialUpgradeUI in the scene! Destroying this object.", this);
            Destroy(gameObject); // Destroy self if the scene is not set up correctly.
        }
        else
        {
            // 5. If everything was found, re-enable the collider so the player can pick it up.
            GetComponent<Collider>().enabled = true;
            Debug.Log("SpecialUpgradeGiver initialized successfully and is ready.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats player = other.GetComponent<PlayerStats>();
            if (player != null)
            {
                this.triggeredPlayerStats = player; 
                GenerateAndShowSpecialUpgrade();
                GetComponent<Collider>().enabled = false;
            }
        }
    }

    private void GenerateAndShowSpecialUpgrade()
    {
        Rarity chosenRarityEnum = Random.Range(0f, 100f) < mythicalChance ? Rarity.Mythic : Rarity.Shadow;
        
        RarityTier chosenRarityTier = upgradeManager.GetRarityTiers().FirstOrDefault(tier => tier.rarity == chosenRarityEnum);
        if (chosenRarityTier == null)
        {
            Debug.LogError($"Could not find RarityTier data for {chosenRarityEnum}.", this);
            Destroy(gameObject);
            return;
        }

        var availableUpgrades = upgradeManager.GetAvailableUpgrades();
        if (availableUpgrades.Count == 0)
        {
            Destroy(gameObject);
            return;
        }
        StatUpgradeData chosenStatData = availableUpgrades[Random.Range(0, availableUpgrades.Count)];

        generatedUpgrade = new UpgradeManager.GeneratedUpgrade
        {
            BaseData = chosenStatData,
            Rarity = chosenRarityTier,
            Value = Random.Range(chosenStatData.baseValueMin, chosenStatData.baseValueMax) * chosenRarityTier.valueMultiplier
        };

        specialUpgradePanel.Show(generatedUpgrade, this);
        gameManager.RequestPause();
    }
    
    public void ApplyUpgradeAndDestroy()
    {
        if (generatedUpgrade == null || triggeredPlayerStats == null)
        {
            Debug.LogError("ApplyUpgradeAndDestroy was called, but the upgrade or player reference was missing!");
            gameManager.RequestResume();
            Destroy(gameObject);
            return;
        }
        
        ApplyStatToPlayer(triggeredPlayerStats, generatedUpgrade.BaseData.statToUpgrade, generatedUpgrade.Value);
        
        Debug.Log($"Applied Special Upgrade: {generatedUpgrade.BaseData.statToUpgrade} +{generatedUpgrade.Value} ({generatedUpgrade.Rarity.rarity})");

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