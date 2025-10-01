using UnityEngine;

/// <summary>
/// This component is attached to a GameObject managed by the PlayerWeaponManager.
/// It reads the assigned WeaponData and executes the weapon's behavior based on its archetype.
/// </summary>
public class WeaponController : MonoBehaviour
{
    // The ScriptableObject that defines this weapon's properties.
    public WeaponData weaponData;

    // The timer that tracks when the weapon can attack next.
    private float currentCooldown;

    void Start()
    {
        // Set the initial cooldown when the weapon is first equipped.
        currentCooldown = weaponData.cooldown;
    }

    void Update()
    {
        // All weapon archetypes that have a cooldown are now handled by this single timer.
        currentCooldown -= Time.deltaTime;

        // When the cooldown is finished, perform an attack and reset the timer.
        if (currentCooldown <= 0f)
        {
            Attack();
            currentCooldown = weaponData.cooldown;
        }
    }

    /// <summary>
    /// Performs an attack based on the weapon's archetype.
    /// </summary>
    private void Attack()
    {
        // The switch statement determines what "attacking" means for each archetype.
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile:
                // TODO: Implement FireProjectile() logic.
                // FireProjectile();
                break;

            case WeaponArchetype.Whip:
                // TODO: Implement PerformWhipAttack() logic.
                // PerformWhipAttack();
                break;

            case WeaponArchetype.Orbit:
                // For an Orbit weapon, "attacking" means spawning the orbiting instances.
                // These instances will then live for the specified `duration`.
                ActivateOrbitingWeapon();
                break;

            case WeaponArchetype.Aura:
                // TODO: Implement ActivateAura() logic.
                // This would be similar to Orbit, spawning a persistent area of effect
                // that lasts for a set duration.
                // ActivateAura();
                break;

            // Add cases for Laser, Shield, etc. as you implement them.
        }
    }

    /// <summary>
    /// Spawns the orbiting weapon prefabs around the player.
    /// This is called by the Attack() method when the weapon's archetype is Orbit.
    /// </summary>
    private void ActivateOrbitingWeapon()
    {
        Debug.Log("Activating Orbiting Weapon: " + weaponData.weaponName);
        
        // The center of the orbit is the parent of this controller (i.e., the Player's WeaponManager).
        Transform orbitCenter = transform.parent;

        // Calculate the angle between each weapon to space them out evenly.
        float angleStep = 360f / weaponData.amount;

        for (int i = 0; i < weaponData.amount; i++)
        {
            // Determine the starting angle for this specific instance.
            float startingAngle = i * angleStep;
            
            // Instantiate the weapon prefab and parent it to the orbit center to keep the hierarchy clean.
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);

            // Get the OrbitingWeapon component from the newly created prefab.
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();

            // Pass all the necessary data from the ScriptableObject to the instance.
            if (orbiter != null)
            {
                orbiter.Initialize(weaponData, orbitCenter, startingAngle);
            }
            else
            {
                // This error is crucial for debugging if you forget to attach the script to your prefab.
                Debug.LogError("The weaponPrefab for " + weaponData.name + " is missing an OrbitingWeapon script!");
            }
        }
    }
}