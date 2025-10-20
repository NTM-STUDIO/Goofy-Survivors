using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyProjectileDamage3D : MonoBehaviour
{
    private EnemyStats _casterStats;
    public EnemyStats CasterStats
    {
        get { return _casterStats; }
        set
        {
            _casterStats = value;
            // --- DEBUG ---
            if (_casterStats != null)
            {
                Debug.Log($"[Projectile Debug] Stats received from caster: '{_casterStats.gameObject.name}'. Damage set to: {_casterStats.baseDamage}", gameObject);
            }
            else
            {
                Debug.LogError("[Projectile Debug] CRITICAL: CasterStats were set to NULL!", gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // --- DEBUG ---
            Debug.Log($"[Projectile Debug] Collided with player: '{other.name}'", gameObject);

            var playerStats = other.GetComponentInParent<PlayerStats>();

            if (playerStats != null && CasterStats != null)
            {
                Debug.Log($"[Projectile Debug] Applying {CasterStats.baseDamage} damage to the player.", gameObject);
                playerStats.ApplyDamage(CasterStats.baseDamage, transform.position, 0f);
            }
            else
            {
                if (playerStats == null)
                    Debug.LogWarning($"[Projectile Debug] Collision with '{other.name}' but it has no PlayerStats component.", gameObject);
                if (CasterStats == null)
                    Debug.LogError("[Projectile Debug] CRITICAL: Tried to apply damage but CasterStats are NULL!", gameObject);
            }

            Destroy(gameObject);
        }
    }
}