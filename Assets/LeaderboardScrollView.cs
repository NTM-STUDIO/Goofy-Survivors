using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq; // for sorting

public class LeaderboardScrollView : MonoBehaviour
{
    [System.Serializable]
    public class LeaderboardEntryData
    {
        public string playerName;
        public int score;
    }

    [Header("UI References")]
    [SerializeField] private GameObject entryPrefab;   // Prefab for each leaderboard entry
    [SerializeField] private Transform contentParent;  // Usually "Content" under "Viewport"
    [SerializeField] private ScrollRect scrollRect;    // The Scroll View component

    private readonly List<GameObject> spawnedEntries = new List<GameObject>();

    private void Start()
    {
        // Example data
        List<LeaderboardEntryData> sampleData = new List<LeaderboardEntryData>()
        {
            new LeaderboardEntryData(){ playerName="Alice", score=1200 },
            new LeaderboardEntryData(){ playerName="Bob", score=1100 },
            new LeaderboardEntryData(){ playerName="Cathy", score=950 },
            new LeaderboardEntryData(){ playerName="David", score=800 },
            new LeaderboardEntryData(){ playerName="Eve", score=700 },
        };

        Populate(sampleData);
    }

    public void Populate(List<LeaderboardEntryData> entries)
    {
        // Sort entries by score (highest first)
        entries = entries.OrderByDescending(e => e.score).ToList();

        // Clear existing
        foreach (var go in spawnedEntries)
            Destroy(go);
        spawnedEntries.Clear();

        // Create new entries
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            int rank = i + 1; // rank starts at 1

            GameObject newEntry = Instantiate(entryPrefab, contentParent);
            newEntry.transform.localScale = Vector3.one;

            // Fill in the data
            LeaderboardEntryUI entryUI = newEntry.GetComponent<LeaderboardEntryUI>();
            if (entryUI != null)
            {
                entryUI.SetData(rank, entry.playerName, entry.score);
            }

            spawnedEntries.Add(newEntry);
        }
    }
}
