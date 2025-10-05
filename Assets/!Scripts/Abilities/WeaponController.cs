using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
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
        // --- ESTA É A MUDANÇA ---
        // Ao definir o cooldown inicial para 0, o primeiro ataque será instantâneo.
        currentCooldown = 1f;
    }

    void Update()
    {
        // All weapon archetypes that have a cooldown are now handled by this single timer.
        currentCooldown -= Time.deltaTime;

        // When the cooldown is finished, perform an attack and reset the timer.
        if (currentCooldown <= 0f)
        {
            Attack();
            // Depois do primeiro ataque, o cooldown é definido para o seu valor normal,
            // estabelecendo o ciclo de ataque regular.
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
        Transform orbitCenter = transform.parent;
        float angleStep = 360f / weaponData.amount;
        float randomGroupRotation = UnityEngine.Random.Range(0f, 360f);

        for (int i = 0; i < weaponData.amount; i++)
        {
            float startingAngle = randomGroupRotation + (i * angleStep);

            // --- ESTA É A MUDANÇA PRINCIPAL ---

            // 1. Calcula o vetor de direção a partir do ângulo.
            // Mathf.Deg2Rad converte graus para radianos, que é o que as funções de seno e cosseno usam.
            Vector3 direction = new Vector3(Mathf.Cos(startingAngle * Mathf.Deg2Rad), Mathf.Sin(startingAngle * Mathf.Deg2Rad), 0);

            // 2. Calcula a posição final da arma.
            // Posição Final = Posição do Centro + Direção * Raio
            // Assumindo que o raio está em `weaponData.radius`. Se o nome for diferente, ajuste aqui.
            Vector3 spawnPosition = orbitCenter.position + direction * weaponData.area * 4f; // Multiplicador de 4 para ajustar escala visual

            // 3. Gera uma rotação visual aleatória para o sprite.
            Quaternion randomSpriteRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

            // 4. Instancia o objeto DIRETAMENTE na sua posição orbital final.
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, spawnPosition, randomSpriteRotation, orbitCenter);

            // --- FIM DA MUDANÇA ---

            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                // Agora o Initialize não precisa de mover o objeto, apenas de guardar os dados para a rotação contínua.
                orbiter.Initialize(weaponData, orbitCenter, startingAngle);
            }
        }
    }
}