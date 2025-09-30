// PanController.cs
using UnityEngine;
using System.Collections.Generic;

public class PanController : MonoBehaviour
{
    [Header("Weapon Settings")]
    public GameObject panPrefab;
    public int numberOfPans = 1;         // How many pans to spawn
    public float rotationSpeed = 100f;   // How fast the pans orbit
    public float orbitDistance = 2f;     // How far from the player the pans are
    public float cooldownTime = 5f;      // Time until a new set of pans is spawned

    private float currentCooldown;
    private List<GameObject> activePans = new List<GameObject>();
    private float currentAngle = 0f;

    void Start()
    {
        // Start with the weapon ready to fire
        currentCooldown = 0f;
    }

    void Update()
    {
        // Cooldown timer
        currentCooldown -= Time.deltaTime;

        // When the cooldown is over, spawn the pans
        if (currentCooldown <= 0f)
        {
            SpawnPans();
            currentCooldown = cooldownTime; // Reset the cooldown
        }

        // Handle the orbiting motion of the active pans
        OrbitPans();
    }

    void SpawnPans()
    {
        // Before spawning new pans, destroy the old ones
        foreach (var pan in activePans)
        {
            if (pan != null)
            {
                Destroy(pan);
            }
        }
        activePans.Clear();

        // Spawn the new set of pans
        float angleStep = 360f / numberOfPans;
        for (int i = 0; i < numberOfPans; i++)
        {
            // We use an empty parent GameObject for smoother rotation
            GameObject panOrbitParent = new GameObject("PanOrbitParent");
            panOrbitParent.transform.position = transform.position;

            // Spawn the pan and parent it to the orbit object
            GameObject pan = Instantiate(panPrefab, transform.position, Quaternion.identity, panOrbitParent.transform);
            
            // Set the initial position of the pan
            Vector2 initialPosition = new Vector2(orbitDistance, 0);
            pan.transform.localPosition = initialPosition;

            // Set the initial rotation of the orbit parent
            panOrbitParent.transform.rotation = Quaternion.Euler(0, 0, angleStep * i);
            
            activePans.Add(panOrbitParent);
        }
    }

    void OrbitPans()
    {
        // Rotate each orbit parent around the player
        foreach (var panParent in activePans)
        {
            if (panParent != null)
            {
                // Follow the player's position
                panParent.transform.position = transform.position;
                // Apply rotation
                panParent.transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // This method can be called by a level-up manager
    public void EvolveWeapon()
    {
        numberOfPans++;
        // Optional: Slightly decrease cooldown or increase damage
        // cooldownTime *= 0.9f; 
    }
}