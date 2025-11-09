using UnityEngine;

// Base class for flexible, event-driven rune behaviors.
public abstract class RuneEffect : ScriptableObject
{
    protected PlayerStats owner;

    // Called when the rune is attached to a player's runtime (on spawn).
    public virtual void OnAttach(PlayerStats owner)
    {
        this.owner = owner;
    }

    // Called when the player's runtime is destroyed or the rune is removed.
    public virtual void OnDetach()
    {
        this.owner = null;
    }

    // Optional per-frame tick for dynamic effects (called by RuneRuntime if overridden)
    public virtual void OnTick(float deltaTime) { }
}
