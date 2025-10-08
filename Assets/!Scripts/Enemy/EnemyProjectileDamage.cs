using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectileDamage : MonoBehaviour
{
    [Header("Projectile Damage")]
    public float damage = 10f;      // Dano causado ao player
    public float knockback = 5f;    // Força de knockback (não usada aqui, mas pode ser implementada)
    public bool destroyOnHit = true;// Se verdadeiro, destrói o projétil ao atingir o player

    // Este método é chamado quando o componente é adicionado ou resetado no Inspector
    private void Reset()
    {
        // Garante que o collider é trigger para detectar colisões sem física
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        // Define a tag do projétil para facilitar a identificação nas colisões
        gameObject.tag = "EnemyProjectile";
    }

    // Detecta colisão com outros objetos (trigger)
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Só aplica dano se colidir com o player
        if (other.CompareTag("Player"))
        {
            Debug.Log("Projétil colidiu com o player!");
            // Tenta obter o script PlayerCollision do player
            var playerCollision = other.GetComponent<PlayerCollision>();
            if (playerCollision != null)
            {
                // Tenta obter o script PlayerStats do player
                var playerStats = other.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    // Aplica dano ao player usando o método do sistema de vida
                    playerStats.ApplyDamage(damage, transform.position, null);
                }
            }

            // Se configurado, destrói o projétil após causar dano
            if (destroyOnHit)
                Destroy(gameObject);
        }
    }
}