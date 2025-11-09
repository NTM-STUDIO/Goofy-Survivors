using System;
using System.Reflection;
using UnityEngine;

namespace Infra
{
    /// <summary>
    /// Disables Firebase Realtime Database on-disk persistence very early to avoid
    /// Windows desktop crashes when another instance holds the persistence LOCK file.
    /// This runs before any scene loads to ensure it's applied before other DB usage.
    /// </summary>
    public static class FirebaseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureFirebaseRealtimeDatabase()
        {
            try
            {
                // Only disable on desktop/editor where multiple concurrent instances are common.
                bool isDesktop = Application.isEditor ||
                                 Application.platform == RuntimePlatform.WindowsPlayer ||
                                 Application.platform == RuntimePlatform.OSXPlayer ||
                                 Application.platform == RuntimePlatform.LinuxPlayer;
                if (!isDesktop)
                {
                    return; // keep default behavior on mobile/console
                }

                // Find FirebaseDatabase type without taking a hard compile-time dependency
                var dbType = Type.GetType("Firebase.Database.FirebaseDatabase, Firebase.Database", throwOnError: false);
                if (dbType == null)
                {
                    Debug.Log("[Firebase] Realtime Database SDK not found, skipping persistence config.");
                    return;
                }

                // Get DefaultInstance (same pattern used in official docs)
                var defaultInstanceProp = dbType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static);
                if (defaultInstanceProp == null)
                {
                    Debug.LogWarning("[Firebase] DefaultInstance property not found on FirebaseDatabase.");
                    return;
                }

                var dbInstance = defaultInstanceProp.GetValue(null);
                if (dbInstance == null)
                {
                    Debug.LogWarning("[Firebase] FirebaseDatabase.DefaultInstance returned null.");
                    return;
                }

                // Call instance method SetPersistenceEnabled(false)
                var setPersistence = dbType.GetMethod(
                    "SetPersistenceEnabled",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: new[] { typeof(bool) },
                    modifiers: null);

                if (setPersistence == null)
                {
                    Debug.LogWarning("[Firebase] SetPersistenceEnabled(bool) method not found on FirebaseDatabase instance.");
                    return;
                }

                setPersistence.Invoke(dbInstance, new object[] { false });
                Debug.Log("[Firebase] Realtime Database persistence disabled (desktop/editor). This avoids LOCK file conflicts when multiple instances run.");

                // Optional: if available, we could also shrink cache size if persistence ever gets enabled.
                // var setCacheSize = dbType.GetMethod("SetPersistenceCacheSizeBytes", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(long) }, null);
                // setCacheSize?.Invoke(dbInstance, new object[] { 1 * 1024 * 1024 });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Firebase] Failed to configure Realtime Database persistence: {ex.Message}");
            }
        }
    }
}
