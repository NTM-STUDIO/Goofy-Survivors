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

    public void WriteNewScore(string userId, int damage)
    {
        User newUser = new User(userId, damage);
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
    public string username;
    public int damage;

    public User(string username, int damage)
    {
        this.username = username;
        this.damage = damage;
    }
}
