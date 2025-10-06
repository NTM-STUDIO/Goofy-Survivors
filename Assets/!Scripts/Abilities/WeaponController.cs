using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// This component is attached to a GameObject managed by the PlayerWeaponManager.
/// It reads the assigned WeaponData and executes the weapon's behavior based on its archetype.
/// IT NOW USES PlayerStats TO MODIFY ITS BEHAVIOR.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;
    private float currentCooldown;

    // --- ADDITION 1: REFERENCE TO PLAYER STATS ---
    private PlayerStats playerStats;

    void Start()
    {
        // --- ADDITION 2: FINDING THE PLAYER STATS ---
        #if UNITY_2023_1_OR_NEWER
        playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        if (playerStats == null)
            playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        #else
        // Fallback for older Unity: may be marked obsolete but used under pre-2023
        #pragma warning disable 618
        playerStats = FindObjectOfType<PlayerStats>();
        #pragma warning restore 618
        #endif
        if (playerStats == null)
        {
            Debug.LogError("WeaponController could not find PlayerStats in the scene!");
        }

        // Set initial cooldown to fire immediately.
        currentCooldown = 0f;
    }

    void Update()
    {
        currentCooldown -= Time.deltaTime;

        if (currentCooldown <= 0f)
        {
            Attack();
            // --- CHANGE 1: CALCULATE COOLDOWN USING PLAYER STATS ---
            // Higher attack speed = lower cooldown. That's why we divide.
            float atkSpeed = playerStats != null ? playerStats.attackSpeedMultiplier : 1f;
            currentCooldown = weaponData.cooldown / Mathf.Max(0.0001f, atkSpeed);
        }
    }

    private void Attack()
    {
        // The switch statement remains the same, but the methods it calls will now use player stats.
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile:
                // FireProjectile();
                break;
            case WeaponArchetype.Whip:
                // PerformWhipAttack();
                break;
            case WeaponArchetype.Orbit:
                ActivateOrbitingWeapon();
                break;
            case WeaponArchetype.Aura:
                // ActivateAura();
                break;
        }
    }

    private void ActivateOrbitingWeapon()
    {
        Transform orbitCenter = transform.parent;

        // --- CHANGE 2: CALCULATE AMOUNT USING PLAYER STATS ---
        // We take the weapon's base amount and add the player's bonus projectile count.
    int bonusCount = playerStats != null ? playerStats.projectileCount : 0;
    int currentAmount = Mathf.Max(1, weaponData.amount + bonusCount);

        float angleStep = 360f / currentAmount;
        float randomGroupRotation = UnityEngine.Random.Range(0f, 360f);

        for (int i = 0; i < currentAmount; i++)
        {
            float startingAngle = randomGroupRotation + (i * angleStep);
            Vector3 direction = new Vector3(Mathf.Cos(startingAngle * Mathf.Deg2Rad), Mathf.Sin(startingAngle * Mathf.Deg2Rad), 0);

            // --- CHANGE 3: CALCULATE AREA USING PLAYER STATS ---
            // The weapon's base area is multiplied by the player's size multiplier.
            float sizeMult = playerStats != null ? playerStats.projectileSizeMultiplier : 1f;
            float currentArea = weaponData.area * sizeMult;
            Vector3 spawnPosition = orbitCenter.position + direction * currentArea * 4f;

            Quaternion randomSpriteRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, spawnPosition, randomSpriteRotation, orbitCenter);

            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                // IMPORTANT: The OrbitingWeapon script itself will also need to know about the current damage.
                // We calculate it here and pass it along.
                orbiter.Initialize(weaponData, orbitCenter, startingAngle);

                // --- ADDITION 3: PASSING CALCULATED DAMAGE ---
                // We'll assume the OrbitingWeapon script has a method or property to set its damage.
                // For example: orbiter.damage = weaponData.damage * playerStats.damageMultiplier;
            }
        }
    }
}