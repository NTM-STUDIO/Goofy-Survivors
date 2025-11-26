using UnityEngine;
using Unity.Netcode;

public class ExperienceOrb : NetworkBehaviour
{
    public NetworkVariable<int> netXpValue = new NetworkVariable<int>(10);

    [Header("Settings")]
    public float collectionDistance = 1.5f;
    public float maxSpeed = 50f;
    public float smoothTime = 0.1f;

    private Transform attractionTarget;
    private bool isAttracted = false;
    private Vector3 currentVelocity;
    private bool collected = false;

    public void Setup(int amount)
    {
        if (IsServer) netXpValue.Value = amount;
    }

    void Update()
    {
        if (!isAttracted || attractionTarget == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, attractionTarget.position, ref currentVelocity, smoothTime, maxSpeed);

        if (Vector3.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            CollectOrb();
        }
    }

    public void StartAttraction(Transform target)
    {
        if (isAttracted) return;
        isAttracted = true;
        attractionTarget = target;
    }

    private void CollectOrb()
    {
        if (collected) return;
        collected = true;

        // Esconde visualmente logo (para não parecer lagado)
        foreach(var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach(var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                DistributeAndDestroy();
            }
            else
            {
                // Se for cliente, pede ao servidor.
                // O IsSpawned previne o erro "NullReference __endSendServerRpc"
                if (IsSpawned) RequestCollectServerRpc();
                else Destroy(gameObject); // Se não estiver na rede, mata localmente
            }
        }
        else
        {
            // Singleplayer fallback
            if (attractionTarget != null)
            {
                var ps = attractionTarget.GetComponentInParent<PlayerStats>();
                var px = FindObjectOfType<PlayerExperience>();
                if (ps != null && px != null) px.AddXP(netXpValue.Value * ps.xpGainMultiplier);
            }
            Destroy(gameObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectServerRpc()
    {
        if (!IsSpawned) return;
        DistributeAndDestroy();
    }

    private void DistributeAndDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DistributeSharedXP(netXpValue.Value);
        }
        
        if (IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}