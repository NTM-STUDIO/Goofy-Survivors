using UnityEngine;

// 1. Require 3D components instead of 2D
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Orb Properties")]
    public int xpValue = 10;

    [Header("Fluid Movement")]
    public float smoothTime = 0.1f;
    public float collectionDistance = 2f;
    public float maxSpeed = 50f;

    private Transform attractionTarget;
    private bool isAttracted = false;
    private Vector3 currentVelocity = Vector3.zero;


    void Update()
    {
        if (!isAttracted || attractionTarget == null) return;
        
        // 2. Use Vector3.Distance for 3D space
        if (Vector3.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            CollectOrb();
            return;
        }

        // This SmoothDamp function is already 3D, so it works perfectly.
        // The orb will fly through the air towards the player.
        transform.position = Vector3.SmoothDamp(
            transform.position,
            attractionTarget.position,
            ref currentVelocity,
            smoothTime,
            maxSpeed
        );
    }

    // 3. Use the 3D trigger event with a 3D Collider
    void OnTriggerEnter(Collider other)
    {
        // The logic remains the same, but it's now triggered by a 3D collider.
        if (!isAttracted && other.CompareTag("Items"))
        {
            isAttracted = true;
            attractionTarget = other.transform;
        }
    }

    private void CollectOrb()
    {
        // GetComponentInParent works the same way in 3D, searching up the hierarchy.
        PlayerExperience playerExperience = attractionTarget.GetComponentInParent<PlayerExperience>();
        PlayerStats playerStats = attractionTarget.GetComponentInParent<PlayerStats>();

        if (playerExperience != null && playerStats != null)
        {
            float finalXp = xpValue * playerStats.xpGainMultiplier;
            playerExperience.AddXP(finalXp);
        }
        else
        {
            Debug.LogWarning("ExperienceOrb: Could not find PlayerExperience or PlayerStats on the parent of the attraction target!", attractionTarget);
        }

        Destroy(gameObject);
    }
}