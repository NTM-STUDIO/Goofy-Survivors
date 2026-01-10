using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct DifficultyScalingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<GameDifficultyData>()) return;

        // Pega a referência RW (Read-Write) para poder alterar os dados
        RefRW<GameDifficultyData> difficulty = SystemAPI.GetSingletonRW<GameDifficultyData>();

        difficulty.ValueRW.ElapsedTime += SystemAPI.Time.DeltaTime;

        // Lógica simples: A cada X segundos, aumenta o multiplicador
        float interval = difficulty.ValueRO.DifficultyIncreaseInterval;
        if (interval > 0)
        {
            // Calcula quantos intervalos já passaram
            int intervalsPassed = (int)(difficulty.ValueRO.ElapsedTime / interval);
            
            // Fórmula de Juros Compostos: Base * (Mult ^ Intervalos)
            // Ex: 1.0 * (1.1 ^ 2) = 1.21x
            float newMultiplier = UnityEngine.Mathf.Pow(difficulty.ValueRO.StrengthMultiplier, intervalsPassed);
            
            difficulty.ValueRW.DifficultyMultiplier = newMultiplier;
        }
    }
}
