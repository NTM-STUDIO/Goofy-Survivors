using UnityEngine;
using Unity.Netcode;

// 1. Require 3D components instead of 2D
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NetworkObject))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Orb Properties")]
    public int xpValue = 10;

    [Header("Fluid Movement")]
    public float smoothTime = 0.1f;
    public float collectionDistance = 2f;
    public float maxSpeed = 50f;

    private Transform attractionTarget;
    private bool isAttracted = false;
    private Vector3 currentVelocity = Vector3.zero;
    private bool collected = false;


    void Update()
    {
        if (!isAttracted || attractionTarget == null) return;
        
        // 2. Use Vector3.Distance for 3D space
        if (Vector3.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            if (!collected) CollectOrb();
            return;
        }

        // This SmoothDamp function is already 3D, so it works perfectly.
        // The orb will fly through the air towards the player.
        transform.position = Vector3.SmoothDamp(
            transform.position,
            attractionTarget.position,
            ref currentVelocity,
            smoothTime,
            maxSpeed
        );
    }

    // 3. Use the 3D trigger event with a 3D Collider
    void OnTriggerEnter(Collider other)
    {
        // The logic remains the same, but it's now triggered by a 3D collider.
        if (!isAttracted && other.CompareTag("Items"))
        {
            isAttracted = true;
            attractionTarget = other.transform;
        }
    }

    private void CollectOrb()
    {
        if (collected) return;
        collected = true;

        // Proactively disable pickup collider/visuals immediately to prevent duplicates while the network despawn propagates
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.enabled = false;

        var nm = Unity.Netcode.NetworkManager.Singleton;
        var playerStats = attractionTarget.GetComponentInParent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning("ExperienceOrb: Missing PlayerStats on attraction target's parent.", attractionTarget);
            Destroy(gameObject);
            return;
        }

        // Multiplayer: server awards shared XP to everyone and despawns orb
        if (nm != null && nm.IsListening)
        {
            if (Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                var gm = FindFirstObjectByType<GameManager>();
                if (gm != null)
                {
                    // Pass raw XP; GameManager will scale by shared team multiplier on server
                    gm.DistributeSharedXP(xpValue);
                }

                // Despawn if spawned; otherwise destroy locally to avoid Netcode errors
                var netObj = GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn(true); else Destroy(gameObject);
            }
            else
            {
                // Clients do nothing; server will despawn
            }
            return;
        }

        // Single-player: apply directly and destroy
        var playerExperience = FindFirstObjectByType<PlayerExperience>();
        if (playerExperience != null)
        {
            float finalXp = xpValue * playerStats.xpGainMultiplier;
            playerExperience.AddXP(finalXp);
        }
        Destroy(gameObject);
    }
}