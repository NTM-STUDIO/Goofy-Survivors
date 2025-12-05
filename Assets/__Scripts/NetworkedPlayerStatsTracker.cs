using UnityEngine;
using Unity.Netcode;

// This script's only job is to read the stats from the local PlayerStats component
// and sync them over the network for other players to see.
[RequireComponent(typeof(PlayerStats))]
public class NetworkedPlayerStatsTracker : NetworkBehaviour
{
    // A reference to the local game logic script.
    private PlayerStats localStats;

    // --- SYNCED STATS ---
    // These NetworkVariables will mirror the values in PlayerStats.cs.
    public NetworkVariable<float> ProjectileSize { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> CooldownReduction { get; private set; } = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> Duration { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> Knockback { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> ProjectileCount { get; private set; } = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // CRITICAL STATS - Added for proper synchronization
    public NetworkVariable<int> CurrentHp { get; private set; } = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> MaxHp { get; private set; } = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> DamageMultiplier { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> AttackSpeedMultiplier { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> CritChance { get; private set; } = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> CritDamageMultiplier { get; private set; } = new NetworkVariable<float>(1.5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> MovementSpeed { get; private set; } = new NetworkVariable<float>(5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> IsDowned { get; private set; } = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> TotalDamageDealt { get; private set; } = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> TotalReaperDamageDealt { get; private set; } = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    void Awake()
    {
        // Get the component we will be tracking.
        localStats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        // This entire block only runs on the computer of the player who owns this character.
        if (IsOwner)
        {
            // Read the values from the local script and write them to the synced variables.
            // NetworkVariable is smart and will only send an update if the value has actually changed.
            ProjectileSize.Value = localStats.projectileSizeMultiplier;
            CooldownReduction.Value = localStats.cooldownReduction;
            Duration.Value = localStats.durationMultiplier;
            Knockback.Value = localStats.knockbackMultiplier;
            ProjectileCount.Value = localStats.projectileCount;
            
            // Sync critical stats
            CurrentHp.Value = localStats.CurrentHp;
            MaxHp.Value = localStats.maxHp;
            DamageMultiplier.Value = localStats.damageMultiplier;
            AttackSpeedMultiplier.Value = localStats.attackSpeedMultiplier;
            CritChance.Value = localStats.critChance;
            CritDamageMultiplier.Value = localStats.critDamageMultiplier;
            MovementSpeed.Value = localStats.movementSpeed;
            IsDowned.Value = localStats.IsDowned;
            TotalDamageDealt.Value = localStats.totalDamageDealt;
            TotalReaperDamageDealt.Value = localStats.totalReaperDamageDealt;
        }
    }
}