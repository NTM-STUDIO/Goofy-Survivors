using UnityEngine;
using Unity.Netcode;

public class ExperienceOrb : NetworkBehaviour
{
    // Variável de Rede (Para Multiplayer)
    private readonly NetworkVariable<int> netXpValue = new NetworkVariable<int>(10);
    
    // Variável Local (Para Singleplayer e cache)
    [SerializeField] private int xpValue = 10;

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
        xpValue = amount; // Define localmente
        
        // Se for servidor, define na rede para os clientes saberem
        if (NetworkManager.Singleton != null && IsServer) 
        {
            netXpValue.Value = amount;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Quando o objeto nasce no cliente, atualiza o valor local com o do servidor
        xpValue = netXpValue.Value;
        
        // Ouve alterações futuras (caso o valor mude)
        netXpValue.OnValueChanged += (prev, curr) => xpValue = curr;
    }

    public override void OnNetworkDespawn()
    {
        netXpValue.OnValueChanged -= (prev, curr) => xpValue = curr;
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

        // 1. Esconde visualmente imediatamente
        DisableVisuals();

        // 2. Lógica Multiplayer
        if (GameManager.Instance.isP2P && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                DistributeAndDestroy();
            }
            else
            {
                if (IsSpawned) RequestCollectServerRpc();
                else Destroy(gameObject);
            }
        }
        // 3. Lógica Singleplayer
        else
        {
            // Chama o GameManager diretamente (ele redireciona para o PlayerExperience Global)
            // Passamos o valor BRUTO (xpValue), o PlayerExperience aplica os multiplicadores
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DistributeSharedXP(xpValue);
            }
            
            Destroy(gameObject);
        }
    }

    private void DisableVisuals()
    {
        foreach(var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach(var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCollectServerRpc()
    {
        if (!IsSpawned) return;
        DistributeAndDestroy();
    }

    private void DistributeAndDestroy()
    {
        // Servidor entrega o XP bruto à equipa
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DistributeSharedXP(xpValue);
        }
        
        // Despawn da rede
        if (IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}