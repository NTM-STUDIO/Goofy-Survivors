using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerVisuals : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody rb;

    // Variável de rede para sincronizar a direção (True = Direita, False = Esquerda)
    private NetworkVariable<bool> isFacingRight = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Subscreve à mudança de valor para atualizar visualmente quando muda na rede
        isFacingRight.OnValueChanged += OnFacingDirectionChanged;
    }

    public override void OnNetworkDespawn()
    {
        isFacingRight.OnValueChanged -= OnFacingDirectionChanged;
    }

    private void Update()
    {
        // Só o Dono (o jogador local) decide para onde olha
        if (IsOwner)
        {
            float moveX = Input.GetAxisRaw("Horizontal"); // Ou rb.velocity.x

            if (moveX > 0.1f && !isFacingRight.Value)
            {
                isFacingRight.Value = true; // Olha para a direita
            }
            else if (moveX < -0.1f && isFacingRight.Value)
            {
                isFacingRight.Value = false; // Olha para a esquerda
            }
        }
    }

    private void OnFacingDirectionChanged(bool previous, bool current)
    {
        UpdateSpriteFlip(current);
    }

    private void UpdateSpriteFlip(bool facingRight)
    {
        // Opção A: Usar FlipX do SpriteRenderer
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !facingRight; // Assume que o sprite original olha para a direita
        }

        // Opção B: Se usares Scale (descomenta se preferires este e comenta o de cima)
        /*
        Vector3 scale = transform.localScale;
        scale.x = facingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;
        */
    }
}