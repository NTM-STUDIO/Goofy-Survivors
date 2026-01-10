using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

// Este script vai no prefab da arma (Panela, Projétil, etc.)
// Permite que armas GameObject acertem inimigos híbridos (GameObject + ECS)
public class HybridWeaponDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 10f;
    public float knockback = 5f;
    public bool destroyOnHit = false; // Para projéteis
    public bool canHitMultiple = true; // Para armas orbitais (panela)

    [Header("Cooldown (for multi-hit weapons)")]
    public float hitCooldown = 0.5f;
    
    private System.Collections.Generic.Dictionary<GameObject, float> hitCooldowns = new();

    void OnTriggerEnter(Collider other)
    {
        TryDamage(other.gameObject);
    }

    void OnTriggerStay(Collider other)
    {
        if (canHitMultiple)
        {
            TryDamage(other.gameObject);
        }
    }

    // Para 2D
    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (canHitMultiple)
        {
            TryDamage(other.gameObject);
        }
    }

    private void TryDamage(GameObject target)
    {
        if (!target.CompareTag("Enemy")) return;

        // Verifica cooldown
        if (hitCooldowns.TryGetValue(target, out float lastHit))
        {
            if (Time.time - lastHit < hitCooldown) return;
        }
        hitCooldowns[target] = Time.time;

        // Tenta encontrar o bridge ECS
        var bridge = target.GetComponent<EnemyEntityBridge>();
        if (bridge != null)
        {
            bridge.TakeDamage(damage);
            
            // Aplica knockback visual (opcional)
            ApplyKnockback(target);
        }
        else
        {
            // Fallback: Tenta usar o EnemyStats antigo
            var enemyStats = target.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(damage, false); // (damage, isCritical)
            }
        }

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyKnockback(GameObject target)
    {
        if (knockback <= 0) return;

        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            rb.AddForce(dir * knockback, ForceMode.Impulse);
        }

        var rb2d = target.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            Vector2 dir = (target.transform.position - transform.position).normalized;
            rb2d.AddForce(dir * knockback, ForceMode2D.Impulse);
        }
    }

    void LateUpdate()
    {
        // Limpa cooldowns antigos
        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (var kvp in hitCooldowns)
        {
            if (kvp.Key == null || Time.time - kvp.Value > hitCooldown * 2)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            hitCooldowns.Remove(key);
        }
    }
}
