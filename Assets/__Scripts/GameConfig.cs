using UnityEngine;
using Firebase.Database;
using System.Threading.Tasks;

public class GameConfig : MonoBehaviour
{
    public static GameConfig Instance { get; private set; }

    public float PlayerBaseDamage { get; private set; } = 10f; // Default value

    private db database;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            database = FindFirstObjectByType<db>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task FetchConfig()
    {
        if (database == null)
        {
            Debug.LogError("Database not found, using default config.");
            return;
        }

        var snapshot = await database.MDatabase.Child("gameConfig").GetValueAsync();
        if (snapshot.Exists)
        {
            var configDict = (System.Collections.Generic.IDictionary<string, object>)snapshot.Value;
            if (configDict.ContainsKey("playerBaseDamage"))
            {
                PlayerBaseDamage = System.Convert.ToSingle(configDict["playerBaseDamage"]);
                Debug.Log($"Successfully fetched remote config. Player Base Damage: {PlayerBaseDamage}");
            }
        }
        else
        {
            Debug.LogWarning("No 'gameConfig' found in database. Using default values.");
        }
    }
}
