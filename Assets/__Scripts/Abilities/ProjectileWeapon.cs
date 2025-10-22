using UnityEngine;

// Adiciona um Rigidbody automaticamente e garante que o script não pode ser
// adicionado a um objeto sem um.
[RequireComponent(typeof(Rigidbody))]
public class ProjectileWeapon : MonoBehaviour
{
    // --- Stats passadas pelo WeaponController ---
    private float damage;
    private float speed;
    private float knockbackForce;
    private int pierceCount;

    // --- Variáveis privadas ---
    private Rigidbody rb;
    private Vector3 direction; // MUDANÇA: Agora é um Vector3
    private Transform target; 
    private float lifetime;

    private void Awake()
    {
        // Pega a referência ao Rigidbody no início.
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ProjectileWeapon precisa de um componente Rigidbody!", gameObject);
        }
    }

    /// <summary>
    /// Inicializa as propriedades do projétil.
    /// </summary>
    public void Initialize(Transform targetEnemy, Vector3 initialDirection, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        this.target = targetEnemy;
        this.direction = initialDirection.normalized; // MUDANÇA: Recebe um Vector3
        this.damage = finalDamage;
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.pierceCount = finalPierce;
        this.lifetime = finalDuration;

        transform.localScale *= finalSize;

        // Se tivermos uma direção válida, aponta o projétil para ela.
        if (direction != Vector3.zero)
        {
            // MUDANÇA: Usa LookRotation para apontar na direção do movimento em 3D.
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    void Update()
    {
        // --- Controlo da Duração ---
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        
        // --- Lógica de Homing (Perseguição) ---
        // Se o alvo existe, atualiza a direção para o perseguir.
        if (target != null)
        {
            direction = (target.position - transform.position).normalized;

            // Atualiza a rotação para continuar a "olhar" para o alvo.
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private void FixedUpdate()
    {
        // --- Movimento ---
        // Usamos FixedUpdate para manipulação de física (Rigidbody).
        // Isto resulta num movimento mais suave e consistente.
        if (speed > 0 && rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
    }

    // MUDANÇA: Usa OnTriggerEnter e Collider para a física 3D.
    void OnTriggerEnter(Collider other)
    {
        // Verifica se colidimos com um inimigo.
        if (other.CompareTag("Enemy"))
        {
            EnemyStats enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // Aplica o dano.
                enemyStats.TakeDamage((int)damage);

                // MUDANÇA: Calcula a direção do knockback em 3D.
                Vector3 knockbackDirection = (other.transform.position - transform.position).normalized;
                knockbackDirection.y = 0; // Garante que o knockback é apenas horizontal.
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);
                
                // --- LÓGICA DE PIERCE CORRIGIDA ---
                pierceCount--; // Reduz o número de inimigos que pode perfurar.
                if (pierceCount <= 0)
                {
                    Destroy(gameObject); // Destrói o projétil quando o pierce acaba.
                }
            }
        }

    }
}