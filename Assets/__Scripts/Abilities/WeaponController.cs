using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;

public class WeaponController : MonoBehaviour
{
    public WeaponData WeaponData { get; private set; }

    private int weaponId;
    private PlayerWeaponManager weaponManager;
    private PlayerStats playerStats;
    private WeaponRegistry weaponRegistry;
    private bool isWeaponOwner;
    private float currentCooldown;
    private bool meleeSpawned = false; // Flag para armas melee permanentes

    void Awake()
    {
        // Debug.Log($"[WC-AWAKE] WeaponController for '{this.gameObject.name}' created.");
    }

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
            // CDR reduces cooldown: 0.3 CDR = 30% faster = cooldown * (1 - 0.3) = cooldown * 0.7
            float cdrMultiplier = 1f - Mathf.Clamp(playerStats.cooldownReduction, 0f, 0.9f);
            currentCooldown = WeaponData.cooldown * Mathf.Max(0.1f, cdrMultiplier);
        }
    }

    private void Attack()
    {
        if (WeaponData.archetype == WeaponArchetype.ShadowCloneJutsu)
        {
            SpawnShadowClone();
            return;
        }

        // Melee é sempre local (a arma fica anexada ao jogador)
        if (WeaponData.archetype == WeaponArchetype.Melee)
        {
            if (!meleeSpawned)
            {
                PerformMeleeAttack();
                meleeSpawned = true;
            }
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
        else if (isWeaponOwner)
        {
            // SINGLEPLAYER: Dispara localmente
            FireLocally();
        }
    }

    private void FireLocally()
    {
        // --- 1. PROJÉTEIS ---
        if (WeaponData.archetype == WeaponArchetype.Projectile)
        {
            int finalAmount = WeaponData.amount + playerStats.projectileCount;
            Transform[] targets = GetTargets(finalAmount);
            DamageResult damageResult = playerStats.CalculateDamage(WeaponData.damage);

            // Attack Speed now controls projectile travel speed
            float finalSpeed = WeaponData.speed * playerStats.attackSpeedMultiplier;
            float finalDuration = WeaponData.duration * playerStats.durationMultiplier;
            float finalKnockback = WeaponData.knockback * playerStats.knockbackMultiplier;
            float finalSize = WeaponData.area * playerStats.projectileSizeMultiplier;
            // Knockback penetration scales with knockback multiplier bonus (0 at 1x, up to ~0.5 at 2x)
            float knockbackPen = Mathf.Clamp01((playerStats.knockbackMultiplier - 1f) * 0.5f);

            int wId = weaponRegistry != null ? weaponRegistry.GetWeaponId(WeaponData) : -1;

            foreach (var target in targets)
            {
                Vector3 direction = (target != null) ? (target.position - playerStats.transform.position).normalized : UnityEngine.Random.insideUnitCircle.normalized;
                direction.y = 0;
                Vector3 spawnPos = playerStats.transform.position + Vector3.up * 2f;

                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, spawnPos, Quaternion.identity);

                // Remove NetworkObject em SP
                SafelyRemoveNetworkObject(projectileObj);

                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.ConfigureSource(wId, WeaponData.weaponName);
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize, knockbackPen);
                    projectile.SetOwnerLocal(playerStats);
                }
            }
        }
        // --- 2. ORBITAIS ---
        else if (WeaponData.archetype == WeaponArchetype.Orbit)
        {
            SpawnOrbitingWeaponsAroundSelf();
        }
        // Nota: Melee é tratado no Attack() diretamente
    }

  // --- LÓGICA MELEE (PITCHFORK) ---
// ... (Métodos anteriores iguais) ...
// ... (restante código igual) ...

    // --- LÓGICA MELEE (PITCHFORK) ---
 // ... (restante código)

    // --- LÓGICA MELEE (PITCHFORK) ---
    private void PerformMeleeAttack()
    {
        // 1. Instancia
        // Usamos Y + 1.5 para a altura
        Vector3 spawnPos = transform.position + new Vector3(0, 1.5f, 0);
        GameObject weaponObj = Instantiate(WeaponData.weaponPrefab, spawnPos, Quaternion.identity);
        
        SafelyRemoveNetworkObject(weaponObj);

        // 2. Define o Pai e Reseta Rotação Local
        weaponObj.transform.SetParent(transform); 
        weaponObj.transform.localPosition = new Vector3(0, 1.5f, 0); // Garante posição relativa
        
        // --- O PAI FICA COM ROTAÇÃO ZERO ---
        weaponObj.transform.localRotation = Quaternion.identity; 

        // 3. Calcula Direção
        Vector3 direction = GetDirectionToClosestEnemy(); 
        direction.y = 0; 

        // 4. Inicializa
        var meleeScript = weaponObj.GetComponent<MeleePitchfork>();
        if (meleeScript != null)
        {
            meleeScript.Initialize(direction, playerStats, WeaponData);
        }
}    
    // ... (restante código igual) ...
    private Vector3 GetDirectionToClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length == 0)
        {
            // Se não houver inimigos, aponta para onde o jogador está a andar ou a olhar
            if (playerStats.TryGetComponent<Rigidbody>(out var rb) && rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                return rb.linearVelocity.normalized;
            }
            return Vector3.right; // Fallback padrão (Direita)
        }

        Transform closestEnemy = null;
        float minDistance = float.MaxValue;
        Vector3 myPos = transform.position;

        // Procura o mais próximo manualmente (mais rápido que ordenar a lista toda)
        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            float dist = (enemy.transform.position - myPos).sqrMagnitude;
            if (dist < minDistance)
            {
                minDistance = dist;
                closestEnemy = enemy.transform;
            }
        }

        if (closestEnemy != null)
        {
            // Retorna o vetor normalizado na direção do inimigo
            return (closestEnemy.position - myPos).normalized;
        }

        return Vector3.right;
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

        // SINGLEPLAYER - Spawn clone at player position but NOT as a child
        // Use world space position, spawn at root
        Vector3 spawnPos = stats.transform.position;
        Quaternion spawnRot = stats.transform.rotation;
        
        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab);
        cloneObj.transform.SetParent(null, true);  // Ensure at root, keep world position
        cloneObj.transform.position = spawnPos;
        cloneObj.transform.rotation = spawnRot;
        
        Debug.Log($"[ShadowClone] Spawned at {cloneObj.transform.position}, parent: {(cloneObj.transform.parent != null ? cloneObj.transform.parent.name : "NULL (root)")}");

        // Remove NetworkObject
        SafelyRemoveNetworkObject(cloneObj);

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
            // 1. Instancia sem pai
            GameObject orbitingWeaponObj = Instantiate(WeaponData.weaponPrefab, transform.position, Quaternion.identity);

            // 2. Remove NetworkObject ANTES de definir o pai
            SafelyRemoveNetworkObject(orbitingWeaponObj);

            if (parentClone != null)
            {
                var tracker = orbitingWeaponObj.AddComponent<CloneWeaponTracker>();
                tracker.SetParentClone(parentClone);
            }

            // 3. Define pai (Seguro agora)
            orbitingWeaponObj.transform.SetParent(transform, false);

            var orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.LocalInitialize(transform, i * angleStep, playerStats, WeaponData);
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

            // 2. Remove NetworkObject IMEDIATAMENTE (Segredo do fix)
            SafelyRemoveNetworkObject(auraObj);

            // 3. Define pai (Seguro agora)
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
        // Se estivermos em Singleplayer puro, remove o NetworkObject para não dar erro ao mudar de pai ou instanciar
        if (GameManager.Instance != null && !GameManager.Instance.isP2P)
        {
            var netObj = obj.GetComponent<NetworkObject>();
            // DestroyImmediate é necessário porque o Destroy normal demora 1 frame
            // e o código a seguir (SetParent) executaria antes do componente sumir.
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