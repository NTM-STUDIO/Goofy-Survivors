using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Firebase Database wrapper. Each client (in multiplayer) has its own instance.
/// This allows each player to save their individual stats independently.
/// NOTE: This is NOT a singleton - multiple instances can coexist in multiplayer scenarios.
/// </summary>
public class db : MonoBehaviour
{
    public DatabaseReference MDatabase;

    void Awake()
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            MDatabase = FirebaseDatabase.DefaultInstance.RootReference;
            Debug.Log($"[db] Firebase DB Initialized for client on GameObject: {gameObject.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[db] Failed to initialize Firebase: {ex.Message}");
        }
    }

    public void NewGoofer(string userId, string username, int damage, IReadOnlyDictionary<string, float> abilityDamage = null)
    {
        if (MDatabase == null)
        {
            Debug.LogWarning("[db] Attempted to write score before Firebase database was initialized.");
            return;
        }

        var payload = new Dictionary<string, object>
        {
            { "username", username },
            { "damage", damage }
        };

        if (abilityDamage != null && abilityDamage.Count > 0)
        {
            var abilityTotals = new Dictionary<string, object>();
            foreach (var kvp in abilityDamage)
            {
                abilityTotals[kvp.Key] = Mathf.RoundToInt(kvp.Value);
            }
            payload["abilities"] = abilityTotals;
        }

        Debug.Log($"[db] Writing to Firebase - User: {username} ({userId}), Damage: {damage}, Abilities: {abilityDamage?.Count ?? 0}");
        
        MDatabase.Child("goofers").Child(userId).SetValueAsync(payload).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[db] Failed to write data for {username}: {task.Exception}");
            }
            else if (task.IsCompleted)
            {
                Debug.Log($"[db] ✅ Successfully saved data for {username} with {damage} damage");
            }
        });
    }

    public async Task<bool> UserExists(string userId)
    {
        var snapshot = await MDatabase.Child("goofers").Child(userId).GetValueAsync();
        return snapshot.Exists;
    }

    public Task<DataSnapshot> GetUserAsync(string userId)
    {
        return MDatabase.Child("goofers").Child(userId).GetValueAsync();
    }

    public Task<DataSnapshot> GetGoofersDataAsync()
    {
        return MDatabase.Child("goofers").GetValueAsync();
    }
}