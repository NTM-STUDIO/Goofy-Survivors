using Unity.Entities;
using UnityEngine;

public partial class DifficultySyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Se não houver DifficultyManager (GameObject), não faz nada
        var diffManager = Object.FindAnyObjectByType<DifficultyManager>();
        if (diffManager == null) return;

        if (!SystemAPI.HasSingleton<GameDifficultyData>()) return;

        RefRW<GameDifficultyData> data = SystemAPI.GetSingletonRW<GameDifficultyData>();
        
        // Sincroniza os valores do Manager (GameObject) para o ECS
        // Assim os Spawners ECS usam a dificuldade correta
        data.ValueRW.DifficultyMultiplier = diffManager.CurrentHealthMult;
        data.ValueRW.StrengthMultiplier = diffManager.CurrentDamageMult;
    }
}
