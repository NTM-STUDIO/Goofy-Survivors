using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CloneAIController : MonoBehaviour
{
    // Referências originais
    private PlayerStats ownerStats;
    private PlayerStats selfStats;
    private ShadowClone parentClone;
    private Rigidbody rb;
    private Transform currentTarget;

    // Variáveis de configuração
    [Header("Configurações")]
    public float thinkInterval = 0.2f;
    public float detectionRadius = 10f;
    public float attackRange = 2f;
    public float followDistance = 3f;
    public float moveSpeedMultiplier = 1f;

    [Header("Otimização")]
    public LayerMask enemyLayer;

    // Variável interna para sincronizar a Coroutine com o FixedUpdate
    private Vector3 intendedVelocity;

    // Novo enum para estados táticos
    private enum CloneState
    {
        Following,      // Seguindo o jogador
        Engaging,       // Lutando com inimigo
        Retreating,     // Recuando (pouca vida)
        Defending       // Defendendo o jogador ativamente
    }

    private CloneState currentState = CloneState.Following;
    private float optimalAttackRange = 2f;
    private bool areWeaponsReady = true;

    public void Initialize(PlayerStats self, PlayerStats owner, ShadowClone clone)
    {
        this.selfStats = self;
        this.ownerStats = owner;
        this.parentClone = clone;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // Match movement constraints
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        
        // Melhoria: Interpolação deixa o movimento suave entre frames físicos
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Calcula o alcance ideal baseado nas armas iniciais
        DetermineWeaponStatus();

        StartCoroutine(ThinkLoop());
    }

    // Adicionado para garantir física suave e correta
    private void FixedUpdate()
    {
        if (rb != null)
        {
            // Aplica a velocidade calculada na lógica de decisão
            rb.linearVelocity = intendedVelocity;
        }
    }

    private IEnumerator ThinkLoop()
    {
        // Otimização: Cache do WaitForSeconds para não gerar lixo de memória (GC)
        var wait = new WaitForSeconds(thinkInterval);

        while (this != null && gameObject != null)
        {
            // Recalcula alcance e status ocasionalmente (frequente porque cooldowns mudam rapido)
            if (Time.frameCount % 15 == 0) // Mais frequente para status
                DetermineWeaponStatus();

            ThinkOnce();
            yield return wait;
        }
    }

    private void DetermineWeaponStatus()
    {
        if (selfStats == null) {
            optimalAttackRange = attackRange;
            areWeaponsReady = true;
            return;
        }
        
        var weaponControllers = selfStats.GetComponentsInChildren<WeaponController>();
        if (weaponControllers.Length == 0) {
            optimalAttackRange = attackRange;
            areWeaponsReady = true;
            return;
        }

        float maxRange = 2f; // Minimo
        bool hasMelee = false;
        bool anyReady = false;

        foreach (var wc in weaponControllers)
        {
            if (wc.WeaponData == null) continue;
            
            if (wc.IsReady) anyReady = true;

            switch (wc.WeaponData.archetype)
            {
                case WeaponArchetype.Projectile:
                case WeaponArchetype.Laser: // Lasers geralmente têm bom alcance
                    maxRange = Mathf.Max(maxRange, 8f);
                    break;
                case WeaponArchetype.Melee:
                case WeaponArchetype.Whip:
                case WeaponArchetype.Shield:
                    hasMelee = true;
                    // Não aumenta o range, queremos ficar perto
                    break;
                case WeaponArchetype.Aura:
                case WeaponArchetype.Orbit:
                    maxRange = Mathf.Max(maxRange, 4f); // Alcance médio
                    break;
            }
        }
        
        // Se tiver melee, prefere ficar mais perto mesmo que tenha armas de longe
        optimalAttackRange = hasMelee ? 2.5f : maxRange;
        areWeaponsReady = anyReady;
    }

    private void ThinkOnce()
    {
        // 0) Verificação de segurança / Estado de Recuo
        if (selfStats != null && selfStats.CurrentHp < selfStats.maxHp * 0.3f)
        {
            currentState = CloneState.Retreating;
            if (ownerStats != null)
            {
                MoveTowards(ownerStats.transform.position);
                return;
            }
        }

        // 1) Detect best target
        Transform bestTarget = FindBestTarget();

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            // Se o alvo está perto do owner, estamos a defender
            if (ownerStats != null && Vector3.Distance(bestTarget.position, ownerStats.transform.position) < 5f)
            {
                currentState = CloneState.Defending;
            }
            else
            {
                currentState = CloneState.Engaging;
            }
        }
        else
        {
            currentTarget = null;
            currentState = CloneState.Following;
        }

        // 2) Decision / Movement
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            
            // Lógica de Cooldown: Se não estivermos prontos para atacar, evitamos o inimigo (kite)
            float effectiveAttackRange = areWeaponsReady ? optimalAttackRange : optimalAttackRange * 1.5f;

            if (dist > effectiveAttackRange)
            {
                MoveTowards(currentTarget.position);
            }
            else
            {
                // Dentro do alcance... mas se nao tivermos cooldown, recuamos!
                bool shouldKite = !areWeaponsReady || (optimalAttackRange > 4f && dist < optimalAttackRange * 0.5f);

                if (shouldKite)
                {
                    // Recuar / Kiting
                    Vector3 awayDir = (transform.position - currentTarget.position).normalized;
                    // Tenta andar em circulo se possível (strafing)
                    Vector3 sideDir = Vector3.Cross(Vector3.up, awayDir);
                    
                    // Mistura recuar e strafe
                    Vector3 kiteDir = (awayDir + sideDir * 0.5f).normalized;
                    
                    MoveTowards(transform.position + kiteDir * 3f); 
                }
                else
                {
                    StopMoving();
                }
            }
        }
        else
        {
            // Sem alvo: seguir o owner
            if (ownerStats != null)
            {
                Vector3 ownerPos = ownerStats.transform.position;
                float dOwner = Vector3.Distance(transform.position, ownerPos);
                if (dOwner > followDistance)
                {
                    MoveTowards(ownerPos);
                }
                else
                {
                    StopMoving();
                }
            }
            else
            {
                StopMoving();
            }
        }
    }

    private Transform FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        Transform bestTarget = null;
        float bestScore = float.MinValue;
        
        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float score = 0f;
            
            // Fator Distância: Prefere inimigos mais próximos (max 10 pts)
            // Score aumenta quanto menor a distância
            score += Mathf.Clamp(10f - distance, 0f, 10f);
            
            // Fator Defesa: Grande bónus para inimigos perto do jogador (Defender o Owner)
            if (ownerStats != null)
            {
                float distToOwner = Vector3.Distance(hit.transform.position, ownerStats.transform.position);
                if (distToOwner < 5f)
                {
                    score += 20f; // Prioridade MAXIMA
                }
            }
            
            // Fator Execução: Bónus para inimigos com pouca vida
            var enemyStats = hit.GetComponent<EnemyStats>();
            if (enemyStats != null && enemyStats.MaxHealth > 0)
            {
                float healthPercent = enemyStats.CurrentHealth / enemyStats.MaxHealth;
                if (healthPercent < 0.3f)
                {
                    score += 5f; // Executar fracos
                }
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = hit.transform;
            }
        }
        
        return bestTarget;
    }

    private void MoveTowards(Vector3 worldTarget)
    {
        Vector3 dir = (worldTarget - transform.position);
        dir.y = 0;
        
        if (dir.sqrMagnitude < 0.0001f)
        {
            StopMoving();
            return;
        }

        dir.Normalize();

        // Detecção de Obstáculos Simples (Raycast)
        float obstacleCheckDist = 1.5f;
        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, obstacleCheckDist))
        {
            // Se o que batemos não é o alvo e não é inimigo (assumindo inimigos na layer correta)
            // Se layer do obstaculo for Default ou Environment (geralmente onde estão paredes)
            if (!IsEnemy(hit.collider)) 
            {
                // Tenta desviar para a direita ou esquerda
                Vector3 rightDir = Vector3.Cross(Vector3.up, dir);
                
                // Verifica se direita está livre
                if (!Physics.Raycast(transform.position, rightDir, obstacleCheckDist))
                {
                     dir = Vector3.Lerp(dir, rightDir, 0.7f).normalized;
                }
                else
                {
                     dir = Vector3.Lerp(dir, -rightDir, 0.7f).normalized;
                }
            }
        }

        float baseSpeed = (selfStats != null ? selfStats.movementSpeed : (ownerStats != null ? ownerStats.movementSpeed : 3f));
        float finalSpeed = baseSpeed * moveSpeedMultiplier;

        intendedVelocity = dir * finalSpeed;
    }

    private bool IsEnemy(Collider col)
    {
        // Verifica se a layer do colisor está na mask de enemyLayer
        return ((1 << col.gameObject.layer) & enemyLayer) != 0;
    }

    private void StopMoving()
    {
        intendedVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.15f);
        // Mostra o range otimizado se estiver a executar, senão o default
        float rangeToShow = Application.isPlaying ? optimalAttackRange : attackRange;
        Gizmos.DrawWireSphere(transform.position, rangeToShow);

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }

    private void OnDestroy()
    {
        StopMoving();
    }
}