using Unity.Entities;
using Unity.Mathematics;

public struct DamageVisualEvent : IBufferElementData
{
    public float3 Position;
    public int Amount;
    public bool IsCritical;
}

public struct DamageVisualsSingleton : IComponentData {}
