using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private TextMeshProUGUI cooldownReductionText;
    [SerializeField] private TextMeshProUGUI durationMultiplierText;
    [SerializeField] private TextMeshProUGUI movementSpeedText;
    [SerializeField] private TextMeshProUGUI luckText;
    [SerializeField] private TextMeshProUGUI xpGainMultiplierText;

    [Header("Stat Icons (RawImage)")]
    [SerializeField] private RawImage maxHpIcon;
    [SerializeField] private RawImage hpRegenIcon;
    [SerializeField] private RawImage damageIcon;
    [SerializeField] private RawImage critChanceIcon;
    [SerializeField] private RawImage critDamageIcon;
    [SerializeField] private RawImage attackSpeedIcon;
    [SerializeField] private RawImage projectileCountIcon;
    [SerializeField] private RawImage projectileSizeIcon;
    [SerializeField] private RawImage cooldownReductionIcon;
    [SerializeField] private RawImage durationIcon;
    [SerializeField] private RawImage movementSpeedIcon;
    [SerializeField] private RawImage luckIcon;
    [SerializeField] private RawImage xpGainIcon;

    private bool iconsInitialized = false;

    private void Start()
    {
        InitializeIconsFromUpgrades();
    }

    private void OnEnable()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
        {
            playerStats = nm.LocalClient.PlayerObject.GetComponent<PlayerStats>();
        }
        
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (playerStats == null) return;

        playerStats.OnHealthChanged += UpdateHealth;

        UpdateAllStatDisplays();
        InitializeIconsFromUpgrades();
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHealth;
        }
    }

    /// <summary>
    /// Busca os icones automaticamente à pool de upgrades disponiveis no UpgradeManager e aplica ao painel de stats.
    /// </summary>
    public void InitializeIconsFromUpgrades()
    {
        if (iconsInitialized) return;
        
        var uManager = UpgradeManager.Instance;
        if (uManager == null || uManager.GetAvailableUpgrades() == null) 
        {
            return;
        }

        var availableUpgrades = uManager.GetAvailableUpgrades();

        foreach (var upgrade in availableUpgrades)
        {
            if (upgrade == null || upgrade.icon == null) continue;

            Texture tex = upgrade.icon.texture;
            
            switch (upgrade.statToUpgrade)
            {
                case StatType.MaxHP: if(maxHpIcon) { maxHpIcon.texture = tex; maxHpIcon.gameObject.SetActive(true); } break;
                case StatType.HPRegen: if(hpRegenIcon) { hpRegenIcon.texture = tex; hpRegenIcon.gameObject.SetActive(true); } break;
                case StatType.DamageMultiplier: if(damageIcon) { damageIcon.texture = tex; damageIcon.gameObject.SetActive(true); } break;
                case StatType.CritChance: if(critChanceIcon) { critChanceIcon.texture = tex; critChanceIcon.gameObject.SetActive(true); } break;
                case StatType.CritDamageMultiplier: if(critDamageIcon) { critDamageIcon.texture = tex; critDamageIcon.gameObject.SetActive(true); } break;
                case StatType.AttackSpeedMultiplier: if(attackSpeedIcon) { attackSpeedIcon.texture = tex; attackSpeedIcon.gameObject.SetActive(true); } break;
                case StatType.ProjectileCount: if(projectileCountIcon) { projectileCountIcon.texture = tex; projectileCountIcon.gameObject.SetActive(true); } break;
                case StatType.ProjectileSizeMultiplier: if(projectileSizeIcon) { projectileSizeIcon.texture = tex; projectileSizeIcon.gameObject.SetActive(true); } break;
                case StatType.CooldownReduction: if(cooldownReductionIcon) { cooldownReductionIcon.texture = tex; cooldownReductionIcon.gameObject.SetActive(true); } break;
                case StatType.DurationMultiplier: if(durationIcon) { durationIcon.texture = tex; durationIcon.gameObject.SetActive(true); } break;
                case StatType.MovementSpeed: if(movementSpeedIcon) { movementSpeedIcon.texture = tex; movementSpeedIcon.gameObject.SetActive(true); } break;
                case StatType.Luck: if(luckIcon) { luckIcon.texture = tex; luckIcon.gameObject.SetActive(true); } break;
                case StatType.XPGainMultiplier: if(xpGainIcon) { xpGainIcon.texture = tex; xpGainIcon.gameObject.SetActive(true); } break;
            }
        }

        iconsInitialized = true;
    }

    public void UpdateAllStatDisplays()
    {
        if (playerStats == null) return;

        UpdateHealth(playerStats.CurrentHp, playerStats.maxHp);

        hpRegenText.text = $"HP Regen: {playerStats.hpRegen:F2}/s";
        damageMultiplierText.text = $"Damage: {playerStats.damageMultiplier * 100:F0}%";
        critChanceText.text = $"Crit Chance: {playerStats.critChance:P1}";
        critDamageMultiplierText.text = $"Crit Damage: {playerStats.critDamageMultiplier * 100:F0}%";
        attackSpeedMultiplierText.text = $"Attack Speed: {playerStats.attackSpeedMultiplier * 100:F0}%";
        projectileCountText.text = $"Projectiles: {playerStats.projectileCount}";
        projectileSizeMultiplierText.text = $"Area/Size: {playerStats.projectileSizeMultiplier * 100:F0}%";
        cooldownReductionText.text = $"CDR: {playerStats.cooldownReduction * 100:F0}%";
        durationMultiplierText.text = $"Duration: {playerStats.durationMultiplier * 100:F0}%";
        movementSpeedText.text = $"Move Speed: {playerStats.movementSpeed}";
        luckText.text = $"Luck: {playerStats.luck}";

        var gm = GameManager.Instance;
        if (gm != null && gm.isP2P)
        {
            xpGainMultiplierText.text = $"XP Gain: {gm.SharedXpMultiplier:P0}";
        }
        else
        {
            xpGainMultiplierText.text = $"XP Gain: {playerStats.xpGainMultiplier:P0}";
        }
    }

    private void UpdateHealth(int current, int max)
    {
        if (maxHpText) maxHpText.text = $"Health: {current} / {max}";
    }
}
