using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Threading.Tasks;

public class db : MonoBehaviour
{
    // --- STEP 1: ADD THE SINGLETON INSTANCE ---
    public static db Instance { get; private set; }

    public DatabaseReference MDatabase;

    // --- STEP 2: IMPLEMENT THE SINGLETON LOGIC IN AWAKE ---
    void Awake()
    {
        // This is the core singleton pattern
        if (Instance != null && Instance != this)
        {
            // If another instance exists, destroy this one to enforce the pattern.
            Destroy(gameObject);
            return; // Stop further execution
        }
        else
        {
            // If this is the first instance, set it as the singleton.
            Instance = this;
            // Make this object persist even when loading new scenes.
            DontDestroyOnLoad(gameObject); 
        }

        // This is your original Awake code, which now only runs on the single, persistent instance.
        Debug.Log("Firebase DB Initialized");
        MDatabase = FirebaseDatabase.DefaultInstance.RootReference;
    }

    public void NewGoofer(string userId, string username, int score, int damage)
    {
        User newUser = new User(username, score, damage);
        string json = JsonUtility.ToJson(newUser);

        MDatabase.Child("goofers").Child(userId).SetRawJsonValueAsync(json);
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

// The User class does not need any changes.
public class User
{
    public string username;
    public int score;
    public int damage;

    public User(string username, int score, int damage)
    {
        this.username = username;
        this.score = score;
        this.damage = damage;
    }
}