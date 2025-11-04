using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class EnemyProjectileDamage3D : MonoBehaviour
{
    public float damage;
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
                //Debug.Log($"[Projectile Debug] Stats received from caster: '{_casterStats.gameObject.name}'. Damage set to: {_casterStats.baseDamage}", gameObject);
            }
            else
            {
                //Debug.LogError("[Projectile Debug] CRITICAL: CasterStats were set to NULL!", gameObject);
            }
        }
    }

    public void Start()
    {
        if (_casterStats == null)
        {
            //Debug.LogWarning("[Projectile Debug] CasterStats have not been set on this projectile!", gameObject);
        }

        // Use caster's effective attack damage if available (includes global difficulty multiplier)
        damage = _casterStats != null ? _casterStats.GetAttackDamage() : 0f;

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // --- DEBUG ---
            Debug.Log($"[Projectile Debug] Collided with player: '{other.name}'", gameObject);

            var netObj = other.GetComponentInParent<NetworkObject>();
            var pstats = other.GetComponentInParent<PlayerStats>();
            if (pstats != null && pstats.IsDowned)
            {
                // Ignore downed players completely
                Destroy(gameObject);
                return;
            }
            var isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isNetworked)
            {
                // In P2P, route damage through the server for authority and mirror to owning client
                if (CasterStats != null && netObj != null && GameManager.Instance != null)
                {
                    float finalDmg = (_casterStats != null) ? _casterStats.GetAttackDamage() : damage;
                    // Always call the server-side damage entrypoint; if we're not server, the server will ignore this direct call
                    if (NetworkManager.Singleton.IsServer)
                    {
                        GameManager.Instance.ServerApplyPlayerDamage(netObj.OwnerClientId, finalDmg, transform.position, 0f);
                    }
                    // Even if not server, allow local projectile to be destroyed for visuals
                }
            }
            else
            {
                // Single-player fallback
                var playerStats = other.GetComponentInParent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.ApplyDamage(damage, transform.position, 0f);
                }
            }

            Destroy(gameObject);
        }
    }
}