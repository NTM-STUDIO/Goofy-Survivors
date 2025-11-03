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
    public NetworkVariable<float> ProjectileSpeed { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> Duration { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> Knockback { get; private set; } = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> ProjectileCount { get; private set; } = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // Add any other stats that need to be visually synced here.

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
            ProjectileSpeed.Value = localStats.projectileSpeedMultiplier;
            Duration.Value = localStats.durationMultiplier;
            Knockback.Value = localStats.knockbackMultiplier;
            ProjectileCount.Value = localStats.projectileCount;
        }
    }
}