using UnityEngine;

/// <summary>
/// Manages the behavior of a spawned shadow clone.
/// The clone is purely visual, has a limited lifespan, and is destroyed when its duration expires.
/// </summary>
public class ShadowClone : MonoBehaviour
{
    /// <summary>
    /// Initializes the clone with its lifespan and visual scale.
    /// </summary>
    /// <param name="duration">How long the clone should exist in seconds.</param>
    /// <param name="size">The scale multiplier for the clone's transform.</param>
    public void Initialize(float duration, float size)
    {
        // Set the clone's size
        transform.localScale *= size;

        // Automatically destroy the clone GameObject after its duration has passed.
        Destroy(gameObject, duration);
    }
}