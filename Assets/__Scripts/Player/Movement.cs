using UnityEngine;
using Unity.Netcode; // ALTERAÇÃO: Adicionar a diretiva do Netcode

// ALTERAÇÃO: Herdar de NetworkBehaviour em vez de MonoBehaviour
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
public class Movement : NetworkBehaviour 
{
    // --- Referências ---
    private Rigidbody rb;
    private PlayerStats playerStats;
    private Vector3 moveInput;
    private const float cameraAngleY = 45f;
    private const float horizontalNerfFactor = 0.56f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerStats = GetComponent<PlayerStats>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.constraints |= RigidbodyConstraints.FreezePositionY;
    }

    void Update()
    {
        // ALTERAÇÃO: A verificação de "propriedade"
        // Este é o passo mais importante. Se este não for o meu jogador, não faço nada.
        if (!IsOwner) return;

        // O resto do Update só corre se eu for o dono deste jogador.
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        // ALTERAÇÃO: Repetir a verificação aqui para a lógica de física.
        if (!IsOwner)
        {
            // Se não sou o dono, não devo controlar a física. 
            // O NetworkTransform irá tratar de sincronizar a posição.
            return;
        }

        // Se não houver input, parar.
        if (moveInput.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // --- A LÓGICA FINAL E CORRETA (Sem alterações aqui) ---

        // PASSO 1: APLICAR O NERF HORIZONTAL MATEMATICAMENTE CORRETO
        Vector3 finalInput = moveInput;
        finalInput.x *= horizontalNerfFactor;

        // PASSO 3: RODAR A DIREÇÃO PURA
        Quaternion rotation = Quaternion.Euler(0, cameraAngleY, 0);
        Vector3 movementDirection = rotation * finalInput;

        // PASSO 4: ABRANDAR AS DIAGONAIS MANUALMENTE
        float diagonalCompensation = 1f;
        if (Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.z) > 0.1f)
            diagonalCompensation = 1f / Mathf.Sqrt(2f);

        rb.linearVelocity = movementDirection * playerStats.movementSpeed * diagonalCompensation;
    }
}