using UnityEngine;
using Unity.Netcode;

public class NetworkEnemyVisuals : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Variável de rede: True = Olha para a Direita, False = Olha para a Esquerda
    // Apenas o Servidor pode escrever (NetworkVariableWritePermission.Server)
    private NetworkVariable<bool> isFacingRight = new NetworkVariable<bool>(true);

    private float lastXPosition;

    private void Awake()
    {
        if (spriteRenderer == null) 
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        // Inicializa a posição anterior
        lastXPosition = transform.position.x;

        // Aplica o estado inicial imediatamente
        UpdateSpriteFlip(isFacingRight.Value);

        // Subscreve para ouvir mudanças vindas do servidor
        isFacingRight.OnValueChanged += OnDirectionChanged;
    }

    public override void OnNetworkDespawn()
    {
        isFacingRight.OnValueChanged -= OnDirectionChanged;
    }

    private void Update()
    {
        // Apenas o Servidor calcula a direção (porque é ele que move o inimigo)
        if (IsServer)
        {
            float currentX = transform.position.x;
            float diff = currentX - lastXPosition;

            // Se se moveu o suficiente para a esquerda
            if (diff < -0.01f && isFacingRight.Value)
            {
                isFacingRight.Value = false; // Muda variável na rede
            }
            // Se se moveu o suficiente para a direita
            else if (diff > 0.01f && !isFacingRight.Value)
            {
                isFacingRight.Value = true; // Muda variável na rede
            }

            lastXPosition = currentX;
        }
    }

    // Executado em TODOS (Server e Clients) quando a variável muda
    private void OnDirectionChanged(bool previous, bool current)
    {
        UpdateSpriteFlip(current);
    }

    private void UpdateSpriteFlip(bool facingRight)
    {
        if (spriteRenderer != null)
        {
            // Se o sprite original olha para a direita:
            // FacingRight = true -> flipX = false
            // FacingRight = false -> flipX = true
            spriteRenderer.flipX = !facingRight;
        }
    }
}