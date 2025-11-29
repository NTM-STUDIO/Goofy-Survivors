using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode; // Necessário

public class WeaponController : MonoBehaviour
{
    public WeaponData WeaponData { get; private set; }

    private int weaponId;
    private PlayerWeaponManager weaponManager;
    private PlayerStats playerStats;
    private WeaponRegistry weaponRegistry;
    private bool isWeaponOwner;
    private float currentCooldown;

    public void Initialize(int id, WeaponData data, PlayerWeaponManager manager, PlayerStats stats, bool owner, WeaponRegistry registry)
    {
        if (data == null)
        {
            Debug.LogError($"[WC-INIT] Null WeaponData for id {id}.");
            return;
        }

        this.weaponId = id;
        this.WeaponData = data;
        this.weaponManager = manager;
        this.playerStats = stats;
        this.isWeaponOwner = owner;
        this.weaponRegistry = registry;

        if (isWeaponOwner)
        {
            if (WeaponData.archetype == WeaponArchetype.Aura)
            {
                ActivateAura();
            }
            else if (WeaponData.archetype == WeaponArchetype.Shield)
            {
                if (weaponManager != null) weaponManager.NotifyShieldActivated(weaponId);
            }
        }
    }

    void Update()
    {
        if (!isWeaponOwner || WeaponData == null || playerStats == null) return;
        if (playerStats.IsDowned) return;
        if (WeaponData.archetype == WeaponArchetype.Aura || WeaponData.archetype == WeaponArchetype.Shield) return;

        currentCooldown -= Time.deltaTime;
        if (currentCooldown <= 0f)
        {
            Attack();
            currentCooldown = WeaponData.cooldown / Mathf.Max(0.01f, playerStats.attackSpeedMultiplier);
        }
    }

    private void Attack()
    {
        if (WeaponData.archetype == WeaponArchetype.ShadowCloneJutsu)
        {
            SpawnShadowClone();
            return;
        }

        // Lógica de Disparo
        if (weaponManager != null && GameManager.Instance.isP2P)
        {
            // MULTIPLAYER: Pede ao Manager
            int finalAmount = WeaponData.amount + playerStats.projectileCount;
            Transform[] targets = GetTargets(finalAmount);
            weaponManager.PerformAttack(weaponId, targets);
        }
        else
        {
            // SINGLEPLAYER: Dispara localmente
            if (isWeaponOwner)
            {
                if (WeaponData.archetype == WeaponArchetype.Orbit)
                {
                    SpawnOrbitingWeaponsAroundSelf();
                }
                else if (WeaponData.archetype == WeaponArchetype.Projectile)
                {
                    FireLocally();
                }
            }
        }
    }

    private void SpawnShadowClone()
    {
        if (WeaponData == null || WeaponData.weaponPrefab == null) return;
        if (weaponRegistry == null) return;

        PlayerStats stats = weaponManager != null ? weaponManager.GetComponent<PlayerStats>() : GetComponentInParent<PlayerStats>();
        if (stats == null) return;

        // MULTIPLAYER
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (weaponManager == null) return;

            List<WeaponData> owned = weaponManager.GetOwnedWeapons();
            if (owned == null) return;

            List<int> weaponIdsToClone = owned
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .Select(w => weaponRegistry.GetWeaponId(w))
                .Where(id => id != -1)
                .ToList();

            weaponManager.NotifyShadowCloneActivated(weaponId, weaponIdsToClone.ToArray());
            return;
        }

        // SINGLEPLAYER
        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, stats.transform.position, stats.transform.rotation);
        
        // --- CORREÇÃO SP: Limpa NetworkObject ---
        SafelyRemoveNetworkObject(cloneObj);
        // ----------------------------------------

        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();
        if (cloneScript == null) cloneScript = cloneObj.AddComponent<ShadowClone>();

        List<WeaponData> weaponsToClone;
        if (weaponManager != null)
        {
            weaponsToClone = weaponManager
                .GetOwnedWeapons()
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .ToList();
        }
        else
        {
            weaponsToClone = new List<WeaponData> { WeaponData };
        }

        cloneScript.Initialize(weaponsToClone, stats, weaponRegistry);
    }

    private void SpawnOrbitingWeaponsAroundSelf()
    {
        int finalAmount = WeaponData.amount + playerStats.projectileCount;
        float angleStep = 360f / Mathf.Max(1, finalAmount);
        ShadowClone parentClone = GetComponentInParent<ShadowClone>();

        for (int i = 0; i < finalAmount; i++)
        {
            GameObject orbitingWeaponObj = Instantiate(WeaponData.weaponPrefab, transform.position, Quaternion.identity);
            
            // --- CORREÇÃO SP: Limpa NetworkObject ---
            SafelyRemoveNetworkObject(orbitingWeaponObj);
            // ----------------------------------------

            if (parentClone != null)
            {
                var tracker = orbitingWeaponObj.AddComponent<CloneWeaponTracker>();
                tracker.SetParentClone(parentClone);
            }
            
            orbitingWeaponObj.transform.SetParent(transform, false);
            
            var orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null) orbiter.LocalInitialize(transform, i * angleStep, playerStats, WeaponData);
        }
    }

    private void FireLocally()
    {
        if (WeaponData.archetype == WeaponArchetype.Projectile)
        {
            int finalAmount = WeaponData.amount + playerStats.projectileCount;
            Transform[] targets = GetTargets(finalAmount);
            DamageResult damageResult = playerStats.CalculateDamage(WeaponData.damage);
            
            float finalSpeed = WeaponData.speed * playerStats.projectileSpeedMultiplier;
            float finalDuration = WeaponData.duration * playerStats.durationMultiplier;
            float finalKnockback = WeaponData.knockback * playerStats.knockbackMultiplier;
            float finalSize = WeaponData.area * playerStats.projectileSizeMultiplier;
            
            int wId = weaponRegistry != null ? weaponRegistry.GetWeaponId(WeaponData) : -1;

            foreach (var target in targets)
            {
                Vector3 direction = (target != null) ? (target.position - playerStats.transform.position).normalized : new Vector3(Random.insideUnitCircle.normalized.x, 0, Random.insideUnitCircle.normalized.y);
                direction.y = 0;
                Vector3 spawnPosWithOffset = playerStats.transform.position + Vector3.up * 2f;
                
                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, spawnPosWithOffset, Quaternion.identity);
                
                // --- CORREÇÃO SP: Limpa NetworkObject ---
                SafelyRemoveNetworkObject(projectileObj);
                // ----------------------------------------

                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.ConfigureSource(wId, WeaponData.weaponName);
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                    projectile.SetOwnerLocal(playerStats);
                }
            }
        }
    }

    private void ActivateAura()
    {
        if (WeaponData.weaponPrefab == null) return;
        bool isP2P = GameManager.Instance != null && GameManager.Instance.isP2P;

        if (!isP2P)
        {
            // SINGLEPLAYER:
            Transform parent = weaponManager != null ? weaponManager.transform : (transform.parent != null ? transform.parent : transform);
            
            // 1. Instancia sem pai
            GameObject auraObj = Instantiate(WeaponData.weaponPrefab, parent.position, Quaternion.identity);
            
            // 2. CORREÇÃO CRÍTICA PARA AURA: Destruir NetworkObject imediatamente
            SafelyRemoveNetworkObject(auraObj);

            // 3. Define pai (Agora seguro)
            auraObj.transform.SetParent(parent);

            AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
            if (aura != null) { aura.Initialize(playerStats, WeaponData); }
        }
        else if (weaponManager != null)
        {
            // MULTIPLAYER: O Manager trata de pedir ao servidor
            weaponManager.NotifyAuraActivated(weaponId);
        }
    }

    // --- FUNÇÃO AUXILIAR DE SEGURANÇA ---
    private void SafelyRemoveNetworkObject(GameObject obj)
    {
        if (GameManager.Instance != null && !GameManager.Instance.isP2P)
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null) DestroyImmediate(netObj);
        }
    }

    private Transform[] GetTargets(int amount)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform[] targets = new Transform[amount];
        if (enemies.Length == 0) return targets;
        
        switch (WeaponData.targetingStyle)
        {
            case TargetingStyle.Closest:
                var sorted = enemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).ToArray();
                for (int i = 0; i < amount && i < sorted.Length; i++) targets[i] = sorted[i].transform;
                break;
            case TargetingStyle.Random:
                for (int i = 0; i < amount; i++) targets[i] = enemies[Random.Range(0, enemies.Length)].transform;
                break;
            case TargetingStyle.Strongest:
                var strongest = enemies.OrderByDescending(e => e.GetComponent<EnemyStats>()?.CurrentHealth ?? 0).ToArray();
                for (int i = 0; i < amount && i < strongest.Length; i++) targets[i] = strongest[i].transform;
                break;
        }
        return targets;
    }

    public int GetWeaponId() { return this.weaponId; }

    private void OnDestroy()
    {
        if (weaponManager == null) 
        {
            var orbiters = GetComponentsInChildren<OrbitingWeapon>(true);
            foreach (var orbiter in orbiters)
            {
                if (orbiter != null && orbiter.gameObject != this.gameObject) Destroy(orbiter.gameObject);
            }
            
            var auras = GetComponentsInChildren<AuraWeapon>(true);
            foreach (var aura in auras)
            {
                if (aura != null && aura.gameObject != this.gameObject) Destroy(aura.gameObject);
            }
        }
    }
}