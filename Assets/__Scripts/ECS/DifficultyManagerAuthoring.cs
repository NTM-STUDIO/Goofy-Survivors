using Unity.Entities;

public struct GameDifficultyData : IComponentData
{
    public float ElapsedTime;
    public float DifficultyMultiplier;
    
    // Configurações
    public float DifficultyIncreaseInterval; // ex: 30s
    public float StrengthMultiplier; // ex: 1.1x
}

public class DifficultyManagerAuthoring : UnityEngine.MonoBehaviour
{
    public float DifficultyIncreaseInterval = 30f;
    public float StrengthMultiplier = 1.1f;

    public class Baker : Baker<DifficultyManagerAuthoring>
    {
        public override void Bake(DifficultyManagerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GameDifficultyData
            {
                ElapsedTime = 0,
                DifficultyMultiplier = 1f,
                DifficultyIncreaseInterval = authoring.DifficultyIncreaseInterval,
                StrengthMultiplier = authoring.StrengthMultiplier
            });
        }
    }
}
