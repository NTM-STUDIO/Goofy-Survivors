using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using System.Reflection;

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

    public bool IsReady => currentCooldown <= 0f;

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
        if (weaponManager != null && GameManager.Instance != null && GameManager.Instance.isP2P)
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
        
        SafelyRemoveNetworkObject(weaponObj, true);

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

        // SINGLEPLAYER - Spawn clone at player position with small random offset
        // Check for existing clone limit
        PlayerWeaponManager effectiveManager = weaponManager != null ? weaponManager : stats.GetComponent<PlayerWeaponManager>();
        if (effectiveManager != null && effectiveManager.ActiveShadowCloneLocal != null)
        {
            if (effectiveManager.ActiveShadowCloneLocal.gameObject != null)
                Destroy(effectiveManager.ActiveShadowCloneLocal.gameObject);
            effectiveManager.ActiveShadowCloneLocal = null;
        }

        // Use world space position, spawn at root
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle.normalized * 1.5f;
        Vector3 spawnPos = stats.transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
        Quaternion spawnRot = stats.transform.rotation;
        
        Debug.Log($"[WeaponController] Spawning ShadowClone at {spawnPos} (Player at {stats.transform.position})");
        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, spawnPos, spawnRot);
        
        if (cloneObj.transform.parent != null)
        {
            cloneObj.transform.SetParent(null, true);  // Ensure at root
        }
        
        // Remove NetworkObject
        SafelyRemoveNetworkObject(cloneObj);

        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();
        if (cloneScript == null) cloneScript = cloneObj.AddComponent<ShadowClone>();
        
        // Register new clone
        if (effectiveManager != null) effectiveManager.ActiveShadowCloneLocal = cloneScript;

        List<WeaponData> weaponsToClone;

        // Primeiro tenta obter via weaponManager (normal)
        if (weaponManager != null)
        {
            weaponsToClone = weaponManager
                .GetOwnedWeapons()
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .ToList();
        }
        else
        {
            // fallback: tenta obter PlayerWeaponManager a partir do PlayerStats (stats pode ser o player)
            var playerWeaponManager = stats.GetComponent<PlayerWeaponManager>();
            if (playerWeaponManager != null)
            {
                weaponsToClone = playerWeaponManager
                    .GetOwnedWeapons()
                    .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                    .ToList();
            }
            else
            {
                // último recurso: clona apenas a própria skill (ShadowCloneJutsu) para evitar nulo
                weaponsToClone = new List<WeaponData> { WeaponData };
            }
        }

        cloneScript.Initialize(weaponsToClone, stats, weaponRegistry);
        
        if (effectiveManager != null)
            effectiveManager.ActiveShadowCloneLocal = cloneScript;
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

            // 3. NÃO definir pai para evitar exceção do NetworkObject em SP (orbiting handle movement itself)
            // orbitingWeaponObj.transform.SetParent(transform, false);

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

            // 2. Remove NetworkObject
            SafelyRemoveNetworkObject(auraObj); // Will use Destroy (deferred) if inside physics callback

            // 3. Define pai com atraso para garantir que NetworkObject já foi destruído
            // Isso evita "NotListeningException" se removemos via Destroy() num frame de física
            if (this != null && this.gameObject.activeInHierarchy) {
                StartCoroutine(ParentAfterDelay(auraObj, parent));
            } else {
                // Fallback se não puder iniciar coroutine (raro)
                try { auraObj.transform.SetParent(parent); } catch {}
            }

            AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
            if (aura != null) { aura.Initialize(playerStats, WeaponData); }
        }
        else if (weaponManager != null)
        {
            // MULTIPLAYER: O Manager trata de pedir ao servidor
            weaponManager.NotifyAuraActivated(weaponId);
        }
    }

    private System.Collections.IEnumerator ParentAfterDelay(GameObject child, Transform parent)
    {
        // Espera um frame para o Destroy(component) processar e remover o NetworkObject
        yield return null;
        if (child != null && parent != null)
        {
            child.transform.SetParent(parent);
            child.transform.localPosition = Vector3.zero; // Opcional: Centrar
        }
    }

    // --- FUNÇÃO AUXILIAR DE SEGURANÇA ---
    private void SafelyRemoveNetworkObject(GameObject obj, bool immediate = false)
    {
        // Se estivermos em Singleplayer puro, remove o NetworkObject para não dar erro ao mudar de pai ou instanciar
        if (GameManager.Instance != null && !GameManager.Instance.isP2P)
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // Se outro componente no objeto declarar [RequireComponent(typeof(NetworkObject))],
                // Unity não permite remover o componente (provoca o erro que vimos).
                // Verificamos por dependências e, se existirem, apenas desativamos o NetworkObject em vez de removê-lo.
                bool hasRequireDependency = false;
                var components = obj.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var compType = comp.GetType();
                    // Skip the NetworkObject itself
                    if (compType == typeof(NetworkObject)) continue;

                    var reqAttrs = compType.GetCustomAttributes(typeof(RequireComponent), true) as RequireComponent[];
                    if (reqAttrs == null || reqAttrs.Length == 0) continue;

                    foreach (var req in reqAttrs)
                    {
                        if (req == null) continue;
                        // Use reflection to inspect fields of the attribute (m_Type0/m_Type1/m_Type2)
                        var fields = req.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        foreach (var f in fields)
                        {
                            if (!typeof(System.Type).IsAssignableFrom(f.FieldType)) continue;
                            var val = f.GetValue(req) as System.Type;
                            if (val == typeof(NetworkObject))
                            {
                                hasRequireDependency = true;
                                break;
                            }
                        }
                        if (hasRequireDependency) break;
                    }
                    if (hasRequireDependency) break;
                }

                if (!hasRequireDependency)
                {
                    if (immediate) DestroyImmediate(netObj);
                    else Destroy(netObj);
                }
                else
                {
                    // Não podemos remover o componente por causa de dependências; apenas desativamos para evitar lógica de rede
                    try { netObj.enabled = false; } catch { }
                }
            }
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