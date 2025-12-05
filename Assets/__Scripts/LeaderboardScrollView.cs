using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // for sorting
using Firebase.Database;

public class LeaderboardScrollView : MonoBehaviour
{
    [System.Serializable]
    public class LeaderboardEntryData
    {
        public string playerName;
        public int damage;
    }

    [Header("UI References")]
    [SerializeField] private GameObject entryPrefab;   // Prefab for each leaderboard entry
    [SerializeField] private Transform contentParent;  // Usually "Content" under "Viewport"
    [SerializeField] private ScrollRect scrollRect;    // The Scroll View component

    private readonly List<GameObject> spawnedEntries = new List<GameObject>();
    private db database;

    private void OnEnable()
    {
        database = FindFirstObjectByType<db>();
        if (database == null)
        {
            Debug.LogError("db script not found in the scene!");
            // Fallback to sample data if database is not found
            List<LeaderboardEntryData> sampleData = new List<LeaderboardEntryData>()
            {
                new LeaderboardEntryData(){ playerName="Alice", damage=1200 },
            };
            Populate(sampleData);
            return;
        }

        // Use a coroutine to wait for the database to be ready
        StartCoroutine(WaitForDbAndSubscribe());
    }

    private IEnumerator WaitForDbAndSubscribe()
    {
        // Wait until MDatabase is initialized
        yield return new WaitUntil(() => database.MDatabase != null);

        // Now it's safe to subscribe
        database.MDatabase.Child("goofers").ValueChanged += HandleLeaderboardUpdated;
        
        // Also, trigger an initial data load
        var task = database.MDatabase.Child("goofers").GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogError("Failed to get initial leaderboard data: " + task.Exception);
        }
        else if (task.IsCompleted)
        {
            ProcessSnapshot(task.Result);
        }
    }

    private void OnDisable()
    {
        if (database != null && database.MDatabase != null)
        {
            // Unsubscribe to prevent memory leaks
            database.MDatabase.Child("goofers").ValueChanged -= HandleLeaderboardUpdated;
        }

        // Clear existing entries when the object is disabled/destroyed
        ClearAllEntries();
    }

    private void OnDestroy()
    {
        // Ensure cleanup on destroy as well
        ClearAllEntries();
    }

    private void ClearAllEntries()
    {
        foreach (var go in spawnedEntries)
        {
            if (go != null)
                Destroy(go);
        }
        spawnedEntries.Clear();
    }

    private void HandleLeaderboardUpdated(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError(args.DatabaseError.Message);
            return;
        }
        ProcessSnapshot(args.Snapshot);
    }

    private void ProcessSnapshot(DataSnapshot snapshot)
    {
        if (snapshot != null && snapshot.Exists)
        {
            List<LeaderboardEntryData> leaderboardData = new List<LeaderboardEntryData>();

            foreach (DataSnapshot userSnapshot in snapshot.Children)
            {
                var userDict = (IDictionary<string, object>)userSnapshot.Value;
                leaderboardData.Add(new LeaderboardEntryData()
                {
                    playerName = userDict["username"].ToString(),
                    damage = System.Convert.ToInt32(userDict["damage"])
                });
            }

            Populate(leaderboardData);
        }
    }

    private void Start()
    {
        // The logic is now handled by OnEnable and the ValueChanged event
    }

    public void Populate(List<LeaderboardEntryData> entries)
    {
        // Sort entries by damage (highest first)
        entries = entries.OrderByDescending(e => e.damage).ToList();

        // Clear existing entries first
        ClearAllEntries();

        // Validate references before creating entries
        if (entryPrefab == null)
        {
            Debug.LogError("LeaderboardScrollView: entryPrefab is not assigned!");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogError("LeaderboardScrollView: contentParent is not assigned!");
            return;
        }

        // Create new entries
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            int rank = i + 1; // rank starts at 1

            GameObject newEntry = Instantiate(entryPrefab, contentParent);
            
            // CRITICAL: Mark as runtime-only to prevent saving to scene
            newEntry.hideFlags = HideFlags.DontSave;
            
            // Reset transform to ensure proper positioning within parent
            newEntry.transform.localPosition = Vector3.zero;
            newEntry.transform.localRotation = Quaternion.identity;
            newEntry.transform.localScale = Vector3.one;

            // Fill in the data
            LeaderboardEntryUI entryUI = newEntry.GetComponent<LeaderboardEntryUI>();
            if (entryUI != null)
            {
                entryUI.SetData(rank, entry.playerName, entry.damage);
            }
            else
            {
                Debug.LogWarning($"LeaderboardScrollView: Entry at rank {rank} is missing LeaderboardEntryUI component!");
            }

            spawnedEntries.Add(newEntry);
        }
    }
}