using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Object = UnityEngine.Object; // disambiguate from System.Object

// This script provides a way to execute code on the main thread from other threads.
// It's a common utility in Unity when dealing with asynchronous operations like Firebase.
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            // Try to find an existing instance in the scene
            _instance = Object.FindFirstObjectByType<UnityMainThreadDispatcher>();
            if (_instance == null)
            {
                // If not found, create a new GameObject and add the dispatcher component
                GameObject obj = new GameObject("UnityMainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj); // Ensure it persists across scenes
            }
        }
        return _instance;
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
