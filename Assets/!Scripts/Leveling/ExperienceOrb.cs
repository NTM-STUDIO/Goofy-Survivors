using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Orb Properties")]
    public int xpValue = 10; // How much XP this orb gives. Set this differently for each prefab.

    [Header("Movement")]
    public float pickupRadius = 3f; // How close the player needs to be to attract the orb.
    public float moveSpeed = 8f;
    public float dropForce = 5f; // The upward force for the "bounce" animation.

    private Transform player;
    private Rigidbody2D rb;
    private bool isAttracted = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Find the player by their tag. Make sure your player GameObject is tagged "Player".
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Start()
    {
        // Apply a small, random upward bounce when the orb is created.
        float randomX = Random.Range(-0.5f, 0.5f);
        Vector2 forceDirection = new Vector2(randomX, 1).normalized;
        rb.AddForce(forceDirection * dropForce, ForceMode2D.Impulse);
    }

    void Update()
    {
        if (player == null) return;

        // Check distance to the player to start the attraction.
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= pickupRadius)
        {
            isAttracted = true;
        }

        // If attracted, move towards the player.
        if (isAttracted)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // When the orb touches the player...
        if (other.CompareTag("Player"))
        {
            // Find the LevelUpManager in the scene and give it the XP.
            LevelUpManager manager = FindObjectOfType<LevelUpManager>();
            if (manager != null)
            {
                manager.AddXP(xpValue);
            }
            
            // Destroy the orb object.
            Destroy(gameObject);
        }
    }
}