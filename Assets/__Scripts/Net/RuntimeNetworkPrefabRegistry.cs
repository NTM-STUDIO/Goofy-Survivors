using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Centralized guard for registering runtime-spawned prefabs with Netcode without causing duplicate registration errors.
/// - Skips registration if prefab already appears in NetworkConfig.Prefabs
/// - Skips if we've already registered this prefab at runtime in this session
/// </summary>
public static class RuntimeNetworkPrefabRegistry
{
    private static readonly HashSet<GameObject> s_RuntimeRegistered = new HashSet<GameObject>();

    public static void TryRegister(GameObject prefab)
    {
        if (prefab == null) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // If prefab is already in the configured list, do not register again
        var config = nm.NetworkConfig;
        if (config != null && config.Prefabs != null)
        {
            // Attempt to detect if this prefab is already configured (by asset reference)
            try
            {
                foreach (var np in config.Prefabs.Prefabs)
                {
                    if (np == null) continue;
                    if (np.Prefab == prefab || np.SourcePrefabToOverride == prefab)
                    {
                        return; // already configured, skip
                    }
                }
            }
            catch
            {
                // In case API differs, fall back to runtime guard only
            }
        }

        // Avoid double-runtime-registration in this session
        if (s_RuntimeRegistered.Contains(prefab)) return;
        s_RuntimeRegistered.Add(prefab);

        try { nm.AddNetworkPrefab(prefab); }
        catch { /* ignore duplicates on some NGO versions */ }
    }
}
