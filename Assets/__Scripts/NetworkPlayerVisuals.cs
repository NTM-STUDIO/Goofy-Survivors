using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerVisuals : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float directionThreshold = 0.1f; // Threshold para considerar movimento
    
    // Variável de rede: True = Direita, False = Esquerda (apenas servidor escreve)
    private NetworkVariable<bool> isFacingRight = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    
    private Rigidbody rb;
    private bool lastLocalFacing = true;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        isFacingRight.OnValueChanged += OnFacingDirectionChanged;
        UpdateSpriteFlip(isFacingRight.Value); // Atualiza estado inicial
    }

    public override void OnNetworkDespawn()
    {
        isFacingRight.OnValueChanged -= OnFacingDirectionChanged;
    }

    private void Update()
    {
        if (rb == null) return;

        bool networkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        Vector3 velocity = rb.linearVelocity;
        float moveX = velocity.x;
        bool hasMovement = Mathf.Abs(moveX) > directionThreshold;
        bool shouldFaceRight = hasMovement ? moveX > 0f : lastLocalFacing;

        if (networkActive)
        {
            if (IsServer)
            {
                ApplyServerFacing(shouldFaceRight);
            }
            else if (IsOwner)
            {
                // Atualiza localmente para feedback imediato
                if (shouldFaceRight != lastLocalFacing)
                {
                    UpdateSpriteFlip(shouldFaceRight);
                    ReportFacingDirectionServerRpc(shouldFaceRight);
                }
            }
        }
        else
        {
            UpdateSpriteFlip(shouldFaceRight);
        }

        lastLocalFacing = shouldFaceRight;
    }

    private void OnFacingDirectionChanged(bool previous, bool current)
    {
        UpdateSpriteFlip(current);
    }

    private void ApplyServerFacing(bool facingRight)
    {
        if (isFacingRight.Value != facingRight)
        {
            isFacingRight.Value = facingRight;
            UpdateSpriteFlip(facingRight);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportFacingDirectionServerRpc(bool facingRight)
    {
        ApplyServerFacing(facingRight);
    }

    private void UpdateSpriteFlip(bool facingRight)
    {
        if (spriteRenderer != null)
        {
            // Assume que o sprite original olha para a direita. Se for o contrário, remove o '!'
            spriteRenderer.flipX = !facingRight; 
        }
    }
}