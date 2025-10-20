using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
public class Movement : MonoBehaviour
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
    }

    void Update()
    {
        // Apenas guardar o input raw do jogador.
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        // Se não houver input, parar.
        if (moveInput.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // --- A LÓGICA FINAL E CORRETA ---

        // PASSO 1: APLICAR O NERF HORIZONTAL MATEMATICAMENTE CORRETO
        Vector3 finalInput = moveInput;
        finalInput.x *= horizontalNerfFactor; // Aplicar o nerf de 0.866f

        // PASSO 3: RODAR A DIREÇÃO PURA
        Quaternion rotation = Quaternion.Euler(0, cameraAngleY, 0);
        Vector3 movementDirection = rotation * finalInput;

        // PASSO 4: ABRANDAR AS DIAGONAIS MANUALMENTE
        float diagonalCompensation = 1f;
        if (Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.z) > 0.1f)
            diagonalCompensation = 1f / Mathf.Sqrt(2f); // ≈ 0.707

        rb.linearVelocity = movementDirection * playerStats.movementSpeed * diagonalCompensation;

    }
}