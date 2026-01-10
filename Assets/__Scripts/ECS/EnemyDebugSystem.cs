using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// DEBUG: Sistema para verificar se o ECS está a funcionar
public partial class EnemyDebugSystem : SystemBase
{
    private float logTimer = 0f;

    protected override void OnUpdate()
    {
        logTimer += SystemAPI.Time.DeltaTime;
        
        // Log a cada 2 segundos para não spammar
        if (logTimer < 2f) return;
        logTimer = 0f;

        // Conta inimigos
        int enemyCount = 0;
        float3 firstEnemyPos = float3.zero;
        float firstEnemySpeed = 0f;
        bool hasTarget = false;
        float3 targetPos = float3.zero;

        foreach (var (transform, movement, stats) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyMovementData>, RefRO<EnemyStatsData>>().WithAll<EnemyTag>())
        {
            if (enemyCount == 0)
            {
                firstEnemyPos = transform.ValueRO.Position;
                firstEnemySpeed = stats.ValueRO.MoveSpeed;
                hasTarget = movement.ValueRO.HasTarget;
                targetPos = movement.ValueRO.TargetPosition;
            }
            enemyCount++;
        }

        // Verifica se há player
        bool hasPlayer = SystemAPI.HasSingleton<PlayerPositionSingleton>();
        float3 playerPos = float3.zero;
        if (hasPlayer)
        {
            playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;
        }

        Debug.Log($"[ECS DEBUG] Enemies: {enemyCount} | HasPlayer: {hasPlayer} | PlayerPos: {playerPos}");
        if (enemyCount > 0)
        {
            Debug.Log($"[ECS DEBUG] FirstEnemy Pos: {firstEnemyPos} | Speed: {firstEnemySpeed} | HasTarget: {hasTarget} | TargetPos: {targetPos}");
        }
    }
}
