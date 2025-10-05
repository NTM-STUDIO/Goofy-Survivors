using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Orb Properties")]
    public int xpValue = 10;

    [Header("Fluid Movement")]
    public float smoothTime = 0.1f; // Roughly the time it will take to reach the target. A smaller value means faster, snappier movement.
    public float maxSpeed = 50f;   // The maximum speed the orb can travel.
    public float collectionDistance = 2f;

    private Transform attractionTarget; // The shadow's transform
    private bool isAttracted = false;

    // This is a special variable that SmoothDamp uses to track the orb's current velocity.
    private Vector3 currentVelocity = Vector3.zero;


    // All movement logic is now in Update.
    void Update()
    {
        // If the orb isn't attracted, do nothing.
        if (!isAttracted || attractionTarget == null) return;

        // --- DISTANCE CHECK FOR COLLECTION ---
        // First, check if we are close enough to be collected.
        if (Vector2.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            CollectOrb();
            return; // Exit the Update loop immediately after collection
        }

        // --- FLUID MOVEMENT LOGIC ---
        // If we are not close enough, move the orb smoothly towards its target.
        // Vector3.SmoothDamp gradually changes the orb's position towards the target over time.
        transform.position = Vector3.SmoothDamp(
            transform.position,   // Current position
            attractionTarget.position,   // Target position
            ref currentVelocity,  // The current velocity (the function modifies this)
            smoothTime,           // The time to reach the target
            maxSpeed              // The maximum speed allowed
        );
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // This function's only job is to start the attraction process.
        if (!isAttracted && other.CompareTag("Items"))
        {
            isAttracted = true;
            attractionTarget = other.transform; // The target is the shadow
        }
    }

    private void CollectOrb()
    {
        // The attractionTarget is the shadow, but the scripts are on the parent.
        PlayerExperience playerExperience = attractionTarget.GetComponentInParent<PlayerExperience>();
        PlayerStats playerStats = attractionTarget.GetComponentInParent<PlayerStats>();

        if (playerExperience != null && playerStats != null)
        {
            float finalXp = xpValue * playerStats.xpGainMultiplier;
            playerExperience.AddXP(finalXp);
        }
        
        Destroy(gameObject);
    }
}