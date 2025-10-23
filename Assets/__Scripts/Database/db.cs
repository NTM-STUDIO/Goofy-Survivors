using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Threading.Tasks;

public class db : MonoBehaviour
{
    public DatabaseReference MDatabase;

    void Awake()
    {
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
