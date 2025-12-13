using Unity.Entities;

public struct WeaponVisualState : IComponentData
{
    public Entity VisualInstance;
    public bool IsSpawned;
}
