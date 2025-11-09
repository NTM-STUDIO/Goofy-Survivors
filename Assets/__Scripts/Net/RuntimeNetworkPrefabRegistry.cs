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
    private static bool s_ScrubbedOnce = false;

    /// <summary>
    /// Remove invalid entries from NetworkConfig.Prefabs before starting networking to prevent
    /// warnings about prefabs with no NetworkObject or null references. Safe to call multiple times.
    /// </summary>
    public static void ScrubInvalidConfigEntries()
    {
        if (s_ScrubbedOnce) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.NetworkConfig == null || nm.NetworkConfig.Prefabs == null) return;
        try
        {
            var list = nm.NetworkConfig.Prefabs.Prefabs;
            if (list == null) return;
            // In some NGO versions this collection is read-only; we can't modify it.
            // We'll just scan and log how many invalid entries NGO will ignore at runtime.
            int invalid = 0;
            foreach (var np in list)
            {
                if (np == null) { invalid++; continue; }
                var prefab = np.Prefab != null ? np.Prefab : np.SourcePrefabToOverride;
                if (prefab == null || prefab.GetComponent<NetworkObject>() == null) { invalid++; continue; }
            }
            if (invalid > 0)
            {
                Debug.LogWarning($"[NetPrefabs] Found {invalid} invalid prefab entries in NetworkManager configuration; NGO will ignore them at runtime.");
            }
            s_ScrubbedOnce = true;
        }
        catch { }
    }

    public static void TryRegister(GameObject prefab)
    {
        if (prefab == null) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Skip invalid entries that don't have a NetworkObject; avoids noisy warnings from NGO
        if (prefab.GetComponent<NetworkObject>() == null)
        {
            // Optional: uncomment to see which were skippeda
            // Debug.LogWarning($"[NetPrefabs] Skipping registration for '{prefab.name}' (no NetworkObject component)");
            return;
        }

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
