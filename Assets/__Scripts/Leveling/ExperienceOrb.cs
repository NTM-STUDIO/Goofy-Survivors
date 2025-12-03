using UnityEngine;
using Unity.Netcode;

public class ExperienceOrb : NetworkBehaviour
{
    // Variável de Rede (Para Multiplayer)
    private readonly NetworkVariable<int> netXpValue = new NetworkVariable<int>(10);
    private readonly NetworkVariable<bool> netCollected = new NetworkVariable<bool>(false); // Sincroniza estado de coleta
    
    // Variável Local (Para Singleplayer e cache)
    [SerializeField] private int xpValue = 10;

    [Header("Settings")]
    public float collectionDistance = 1.5f;
    public float maxSpeed = 50f;
    public float smoothTime = 0.1f;

    private Transform attractionTarget;
    private bool isAttracted = false;
    private Vector3 currentVelocity;
    private bool localCollected = false; // Flag local para singleplayer

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
        
        // Quando o orbe é coletado na rede, esconde-o em todos os clientes
        netCollected.OnValueChanged += OnCollectedChanged;
    }

    public override void OnNetworkDespawn()
    {
        netXpValue.OnValueChanged -= (prev, curr) => xpValue = curr;
        netCollected.OnValueChanged -= OnCollectedChanged;
    }

    private void OnCollectedChanged(bool prev, bool curr)
    {
        if (curr && !prev)
        {
            // O servidor marcou como coletado, esconde em todos os clientes
            DisableVisuals();
        }
    }

    void Update()
    {
        // Se já foi coletado (via rede ou local), não faz nada
        if (IsCollected) return;
        if (!isAttracted || attractionTarget == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, attractionTarget.position, ref currentVelocity, smoothTime, maxSpeed);

        if (Vector3.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            CollectOrb();
        }
    }

    // Propriedade que verifica se está coletado (rede ou local)
    private bool IsCollected => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
                                 ? netCollected.Value 
                                 : localCollected;

    public void StartAttraction(Transform target)
    {
        if (isAttracted) return;
        isAttracted = true;
        attractionTarget = target;
    }

    private void CollectOrb()
    {
        // Verifica se já foi coletado para evitar duplicados
        if (IsCollected) return;

        // 1. Lógica Multiplayer
        if (GameManager.Instance.isP2P && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                // Marca como coletado na rede PRIMEIRO (previne race conditions)
                netCollected.Value = true;
                DistributeAndDestroy();
            }
            else
            {
                // Cliente pede ao servidor para coletar
                // Esconde localmente para feedback imediato
                DisableVisuals();
                if (IsSpawned) RequestCollectServerRpc();
            }
        }
        // 2. Lógica Singleplayer
        else
        {
            if (localCollected) return;
            localCollected = true;
            
            // Esconde visualmente imediatamente
            DisableVisuals();

            // Chama o GameManager diretamente
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
        
        // Verifica se já foi coletado (previne duplicados de múltiplos clientes)
        if (netCollected.Value) return;
        
        netCollected.Value = true;
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