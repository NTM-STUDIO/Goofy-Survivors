using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))]
public class Movement : NetworkBehaviour 
{
    private Rigidbody rb;
    private PlayerStats playerStats;
    private Vector3 moveInput;
    private const float cameraAngleY = 45f;
    private const float horizontalNerfFactor = 0.56f;

    // Referência ao GameManager
    private GameManager gameManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerStats = GetComponent<PlayerStats>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        gameManager = GameManager.Instance; // Obter referência ao GameManager
        if(gameManager == null)
            Debug.LogError("Movement: GameManager não encontrado!");
    }

    void Update()
    {
        // Somente aplica IsOwner se estivermos em P2P
        if (gameManager != null && gameManager.isP2P && !IsOwner) return;

        // If downed, ignore input entirely
        if (playerStats != null && playerStats.IsDowned)
        {
            moveInput = Vector3.zero;
            return;
        }

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.z = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        if (gameManager != null && gameManager.isP2P && !IsOwner) return;

        // If downed, hard-stop movement immediately
        if (playerStats != null && playerStats.IsDowned)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (moveInput.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 finalInput = moveInput;
        finalInput.x *= horizontalNerfFactor;

        Quaternion rotation = Quaternion.Euler(0, cameraAngleY, 0);
        Vector3 movementDirection = rotation * finalInput;

        float diagonalCompensation = 1f;
        if (Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.z) > 0.1f)
            diagonalCompensation = 1f / Mathf.Sqrt(2f);

    rb.linearVelocity = movementDirection * playerStats.movementSpeed * diagonalCompensation;
    }
}
