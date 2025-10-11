using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    public GameObject projectilePrefab;   // Prefab do projétil
    public Transform firePoint;           // Ponto de disparo
    public float fireRate = 2f;           // Tempo entre disparos (cooldown)
    public float projectileSpeed = 8f;   // Velocidade do projétil
    public float shootRange = 12f;        // Distância Maxima para disparar

    private float fireTimer;              // Temporizador para controlar o cooldown
    private Transform player;             // Referência ao jogador

    void Start()
    {
        // Procura o jogador na cena pela tag "Player" e guarda a referência
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        // Se não houver jogador, não faz nada
        if (player == null) return;

        // Calcula a distância entre o inimigo e o jogador
        float distance = Vector2.Distance(transform.position, player.position);

        // Só dispara se o jogador estiver dentro do range definido
        if (distance <= shootRange)
        {
            fireTimer += Time.deltaTime; // Incrementa o temporizador
            if (fireTimer >= fireRate)   // Se o cooldown terminou
            {
                ShootAtPlayer();         // Dispara
                fireTimer = 0f;          // Reinicia o cooldown
            }
        }
        else
        {
            fireTimer = fireRate; // Reseta cooldown se jogador sair do range
        }
    }

    // Função que instancia e dispara o projétil na direção do jogador
    void ShootAtPlayer()
    {
        // Calcula a direção normalizada do disparo
        Vector2 direction = (player.position - firePoint.position).normalized;
        // Instancia o projétil no ponto de disparo
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        // Aplica velocidade ao projétil se tiver Rigidbody2D
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }
    }
}