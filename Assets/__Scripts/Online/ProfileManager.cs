// Filename: ProfileManager.cs
// Location: _Scripts/ConnectionSystem/Data/
using UnityEngine;

namespace MyGame.ConnectionSystem.Data
{
    public class ProfileManager : MonoBehaviour
    {
        // --- CORRECTION: Added Singleton pattern ---
        public static ProfileManager Instance { get; private set; }

        public string Profile { get; set; } = "default";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}