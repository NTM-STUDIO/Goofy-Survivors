using Unity.Entities;
using Unity.Mathematics;

// Componente ECS para sprites direcionais (substitui RobustIsometricController)
public struct IsometricSpriteData : IComponentData
{
    // Direção atual (0=DownRight, 1=DownLeft, 2=UpRight, 3=UpLeft)
    public int CurrentDirection;
    public int PreviousDirection;
    
    // Flip horizontal
    public bool FlipX;
}

// Componente para guardar referência à entidade visual (filho com sprite)
public struct SpriteVisualReference : IComponentData
{
    public Entity VisualEntity;
}

// Tag para marcar entidades que precisam de atualização visual
public struct NeedsSpriteUpdate : IComponentData { }
