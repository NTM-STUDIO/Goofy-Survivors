using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;

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
            // Debug detalhado do primeiro inimigo
            var stringBuilder = new System.Text.StringBuilder();
            stringBuilder.Append($"[ECS DEBUG] First Enemy Details:\n");
            stringBuilder.Append($"Pos: {firstEnemyPos}\n");
            stringBuilder.Append($"Speed: {firstEnemySpeed}\n");
            stringBuilder.Append($"HasTarget: {hasTarget} | Target: {targetPos}\n");
            
            // Verifica se tem PhysicsVelocity
            bool hasPhysics = false;
            float3 velocity = float3.zero;
            
            foreach (var (vel, tag) in SystemAPI.Query<RefRO<PhysicsVelocity>, RefRO<EnemyTag>>())
            {
                hasPhysics = true;
                velocity = vel.ValueRO.Linear;
                break; // Só o primeiro
            }
            
            stringBuilder.Append($"HasPhysicsVelocity: {hasPhysics}\n");
            if (hasPhysics) stringBuilder.Append($"Current Velocity: {velocity}\n");
            
            Debug.Log(stringBuilder.ToString());
        }
    }
}
