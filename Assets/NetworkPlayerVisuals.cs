using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerVisuals : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    // Variável de rede: True = Direita, False = Esquerda
    // Permissão de escrita: Owner (o dono do jogador controla a sua direção)
    private NetworkVariable<bool> isFacingRight = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        if (IsOwner)
        {
            // Deteta input local e atualiza a variável de rede
            float moveX = Input.GetAxisRaw("Horizontal");

            if (moveX > 0.1f && !isFacingRight.Value)
            {
                isFacingRight.Value = true;
            }
            else if (moveX < -0.1f && isFacingRight.Value)
            {
                isFacingRight.Value = false;
            }
        }
    }

    private void OnFacingDirectionChanged(bool previous, bool current)
    {
        UpdateSpriteFlip(current);
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