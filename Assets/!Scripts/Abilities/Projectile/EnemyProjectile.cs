using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    public GameObject projectilePrefab;   // Prefab do projétil
    public Transform firePoint;           // Ponto de disparo
    public float fireRate = 2f;           // Tempo entre disparos
    public float projectileSpeed = 10f;   // Velocidade do projétil
    public Transform player;              // Referência ao jogador

    private float fireTimer;

    void Update()
    {
        if (player == null) return;

        fireTimer += Time.deltaTime;

        if (fireTimer >= fireRate)
        {
            ShootAtPlayer();
            fireTimer = 0f;
        }
    }

    void ShootAtPlayer()
    {
        Vector2 direction = (player.position - firePoint.position).normalized;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }
    }
}