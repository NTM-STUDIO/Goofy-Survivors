using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;

public class db : MonoBehaviour
{
    public DatabaseReference MDatabase;

    void Awake()
    {
        Debug.Log("Firebase DB Initialized");
        MDatabase = FirebaseDatabase.DefaultInstance.RootReference;
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

        MDatabase.Child("goofers").Child(userId).SetValueAsync(payload);
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