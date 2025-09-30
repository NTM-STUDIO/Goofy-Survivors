using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class WaveEnemy 
{
    public GameObject enemyPrefab; // Prefab of the enemy to spawn
    public int enemyCount; // Number of this enemy to spawn
}

[System.Serializable]
public class Wave 
{
    public string waveName; // Name of the wave (optional)
    public List<WaveEnemy> enemies = new List<WaveEnemy>(); // List of enemy groups in this wave (new List<WaveEnemy>()) -> not null on start
    public float spawnInterval; // Time between spawns in seconds
    public float healthMultiplierPerWave = 0.1f; // Health increase multiplier per wave 
}