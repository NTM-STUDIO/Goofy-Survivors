using UnityEngine;

/// <summary>
/// Handles movement behavior for enemies without attack logic.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MovementBehaviour : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the enemy.")]
    [SerializeField] private float moveSpeed = 5f;

    private Transform player;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
        }
        else
        {
            Debug.LogError("Player not found! Ensure the player has the tag 'Player'.", gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0; // Ensure movement is only in the XZ plane

        rb.linearVelocity = direction * moveSpeed;
    }
}