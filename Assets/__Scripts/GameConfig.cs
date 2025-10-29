using UnityEngine;
using Firebase.Database;
using System.Threading.Tasks;
using System.Collections.Generic;

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
            database = db.Instance; // Usar a instância singleton é mais fiável
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // A configuração é agora buscada automaticamente no arranque
        await FetchConfig();
    }

    public async Task FetchConfig()
    {
        if (database == null)
        {
            Debug.LogError("Database instance not found, using default config.");
            return;
        }

        // Espera de forma segura até que o script 'db' confirme que o Firebase está pronto
        while (!database.IsFirebaseReady)
        {
            await Task.Yield(); // Pausa por um frame
        }

        // Obtém a referência de forma segura através do método público
        DatabaseReference configRef = database.GetConfigReference();
        if (configRef == null)
        {
            Debug.LogError("Could not get a valid reference to 'gameConfig'. Using default values.");
            return;
        }

        var snapshot = await configRef.GetValueAsync();
        if (snapshot.Exists)
        {
            var configDict = snapshot.Value as IDictionary<string, object>;
            if (configDict != null && configDict.ContainsKey("playerBaseDamage"))
            {
                object damageValue = configDict["playerBaseDamage"];
                PlayerBaseDamage = System.Convert.ToSingle(damageValue);
                Debug.Log($"<color=lime>Successfully fetched remote config. Player Base Damage: {PlayerBaseDamage}</color>");
            }
        }
        else
        {
            Debug.LogWarning("No 'gameConfig' found in database. Using default values.");
        }
    }
}