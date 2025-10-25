using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controls the orb collector’s pickup radius based on the player’s pickupRange stat,
/// and allows temporary modifiers (like Magnet).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PickupRadiusController : MonoBehaviour
{
    private SphereCollider sphereCollider;
    private PlayerStats playerStats;

    private readonly List<float> radiusModifiers = new List<float>();

    void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        // Because PlayerStats is on the parent Player GameObject:
        playerStats = GetComponentInParent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("PickupRadiusController: PlayerStats not found on any parent!", this);
            enabled = false;
            return;
        }

        sphereCollider.isTrigger = true;
    }

    void Update()
    {
        // Base radius from PlayerStats + any temporary modifiers
        float baseRadius = playerStats.pickupRange;
        float maxModifier = radiusModifiers.Count > 0 ? radiusModifiers.Max() : 0f;
        sphereCollider.radius = Mathf.Max(baseRadius, maxModifier);
    }

    public void AddRadiusModifier(float value)
    {
        if (value > 0f && !radiusModifiers.Contains(value))
            radiusModifiers.Add(value);
    }

    public void RemoveRadiusModifier(float value)
    {
        if (radiusModifiers.Contains(value))
            radiusModifiers.Remove(value);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (sphereCollider == null)
            sphereCollider = GetComponent<SphereCollider>();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sphereCollider.radius);
    }
#endif
}
