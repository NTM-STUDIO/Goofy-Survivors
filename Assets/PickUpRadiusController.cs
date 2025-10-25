using UnityEngine;

/// <summary>
/// This script controls the radius of a SphereCollider by directly applying the Player's pickupRange stat.
/// It should be placed on a child object of the player that holds the pickup trigger collider.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PickupRadiusController : MonoBehaviour
{
    private SphereCollider sphereCollider;
    private PlayerStats playerStats;

    void Awake()
    {
        // Get the collider on this same GameObject.
        sphereCollider = GetComponent<SphereCollider>();

        // Find the PlayerStats component on the parent object.
        playerStats = GetComponentInParent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("PickupRadiusController could not find PlayerStats on any parent object! This script will be disabled.", this);
            enabled = false;
        }
    }

    void Update()
    {
        // Every frame, set the collider's radius to be the exact value of the player's current pickupRange stat.
        if (playerStats != null)
        {
            sphereCollider.radius = playerStats.pickupRange;
        }
    }

    // Optional: Add a Gizmo to visualize the radius in the editor
    private void OnDrawGizmosSelected()
    {
        if (sphereCollider != null)
        {
            Gizmos.color = Color.cyan;
            // Draw a wire sphere that matches the collider's current radius
            Gizmos.DrawWireSphere(transform.position, sphereCollider.radius);
        }
    }
}