using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MeleePitchfork : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("ARRASTA O OBJETO FILHO (Visuals) PARA AQUI!")]
    [SerializeField] private Transform visualTransform; 

    [Header("Settings")]
    [SerializeField] private float stabDistance = 2.5f; 
    [SerializeField] private AnimationCurve stabCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    [SerializeField] private float rotationSpeed = 10f; // Velocidade de rotação suave para o inimigo

    // Ajuste extra se o sprite estiver virado para cima (põe 90 ou -90 aqui)
    [SerializeField] private float visualRotationOffset = 0f; 

    private PlayerStats ownerStats;
    private WeaponData weaponData;
    private HashSet<GameObject> hitEnemiesThisStab = new HashSet<GameObject>(); 
    private bool isSinglePlayer;
    private bool isInitialized = false;
    private float sizeScale = 1f;
    private float stabDuration = 0.3f;
    private float finalDistance = 2.5f;
    private float attackCooldown = 0f;
    private float currentAngle = 0f;

    public void Initialize(Vector3 direction, PlayerStats stats, WeaponData data)
    {
        ownerStats = stats;
        weaponData = data;
        isSinglePlayer = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        if (visualTransform == null) 
        {
            if (transform.childCount > 0) visualTransform = transform.GetChild(0);
            else visualTransform = transform;
        }

        // Calcula escala baseada nos stats
        sizeScale = weaponData.area * (stats != null ? stats.projectileSizeMultiplier : 1f);
        visualTransform.localScale = Vector3.one * sizeScale;

        // Calcula duração do ataque baseada no attack speed
        float speedStats = (stats != null) ? Mathf.Max(0.1f, stats.attackSpeedMultiplier) : 1f;
        stabDuration = weaponData.duration / speedStats;
        finalDistance = stabDistance * sizeScale;

        // Ângulo inicial na direção passada
        if (direction != Vector3.zero)
        {
            currentAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            visualTransform.localRotation = Quaternion.Euler(0, 0, currentAngle + visualRotationOffset);
        }

        isInitialized = true;
        
        // Começa a atacar infinitamente
        StartCoroutine(InfiniteAttackLoop());
    }

    private void Update()
    {
        if (!isInitialized || visualTransform == null) return;

        // Encontra o inimigo mais próximo e roda suavemente para ele
        Transform closestEnemy = FindClosestEnemy();
        
        if (closestEnemy != null)
        {
            Vector3 dirToEnemy = closestEnemy.position - transform.position;
            dirToEnemy.y = 0;
            
            if (dirToEnemy.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(dirToEnemy.z, dirToEnemy.x) * Mathf.Rad2Deg;
                currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotationSpeed);
                visualTransform.localRotation = Quaternion.Euler(0, 0, currentAngle + visualRotationOffset);
            }
        }
        else
        {
            // Se não há inimigos, aponta na direção do movimento do jogador
            if (ownerStats != null && ownerStats.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0;
                if (vel.sqrMagnitude > 0.1f)
                {
                    float targetAngle = Mathf.Atan2(vel.z, vel.x) * Mathf.Rad2Deg;
                    currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotationSpeed);
                    visualTransform.localRotation = Quaternion.Euler(0, 0, currentAngle + visualRotationOffset);
                }
            }
        }
    }

    private Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return null;

        Transform closest = null;
        float minDist = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            float dist = (enemy.transform.position - myPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy.transform;
            }
        }

        return closest;
    }

    private IEnumerator InfiniteAttackLoop()
    {
        while (true)
        {
            // Espera pelo cooldown do ataque
            float cooldown = weaponData.cooldown / (ownerStats != null ? ownerStats.attackSpeedMultiplier : 1f);
            yield return new WaitForSeconds(cooldown);

            // Limpa lista de inimigos atingidos para este stab
            hitEnemiesThisStab.Clear();

            // Executa a animação de stab
            yield return StartCoroutine(StabRoutine());
        }
    }

    private IEnumerator StabRoutine()
    {
        float timer = 0f;
        Vector3 startLocalPos = Vector3.zero;

        while (timer < stabDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / stabDuration;
            float curveVal = stabCurve.Evaluate(progress);

            // Move o visual na direção local (frente = right porque rodamos em Z)
            visualTransform.localPosition = startLocalPos + (Vector3.right * curveVal * finalDistance);

            yield return null;
        }

        // Volta à posição inicial
        visualTransform.localPosition = startLocalPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (weaponData == null || ownerStats == null) return;
        if (hitEnemiesThisStab.Contains(other.gameObject)) return;

        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyStats>();
            if (enemy != null)
            {
                hitEnemiesThisStab.Add(other.gameObject);
                DamageResult dmg = ownerStats.CalculateDamage(weaponData.damage);

                if (isSinglePlayer || IsServer)
                {
                    enemy.TakeDamageFromAttacker(dmg.damage, dmg.isCritical, ownerStats);
                    
                    // Empurra na direção do visual (para onde a arma aponta)
                    float kb = weaponData.knockback * ownerStats.knockbackMultiplier;
                    // Knockback penetration scales with knockback multiplier bonus
                    float knockbackPen = Mathf.Clamp01((ownerStats.knockbackMultiplier - 1f) * 0.5f);
                    if (kb > 0) enemy.ApplyKnockback(kb, 0.2f, visualTransform.right, knockbackPen);
                }
            }
        }
    }
}