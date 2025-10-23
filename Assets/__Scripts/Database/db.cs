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

    public void NewGoofer(string userId, string username, int damage)
    {
        User newUser = new User(userId, username, damage);
        string json = JsonUtility.ToJson(newUser);

        MDatabase.Child("goofers").Child(userId).SetRawJsonValueAsync(json);
    }

    public Task<DataSnapshot> GetGoofersDataAsync()
    {
        return MDatabase.Child("goofers").GetValueAsync();
    }

}

public class User
{
    public string userId;
    public string username;
    public int damage;

    public User(string userId, string username, int damage)
    {
        this.userId = userId;
        this.username = username;
        this.damage = damage;
    }
}
