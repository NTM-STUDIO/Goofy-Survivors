using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Sistema ECS puro que calcula a direção do sprite baseado no movimento
// Substitui a lógica do RobustIsometricController
[BurstCompile]
[UpdateAfter(typeof(EnemyMovementSystem))]
public partial struct IsometricSpriteSystem : ISystem
{
    // Vetores de direção para comparação (igual ao RobustIsometricController)
    // Red = Right (1, 0, 0)
    // Blue = Forward (0, 0, 1)  
    // Green = Left (-1, 0, 0)
    // Yellow = Back (0, 0, -1)
    private static readonly float3 DirRight = new float3(1, 0, 0);
    private static readonly float3 DirForward = new float3(0, 0, 1);
    private static readonly float3 DirLeft = new float3(-1, 0, 0);
    private static readonly float3 DirBack = new float3(0, 0, -1);

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Pega a posição do player para calcular direção
        if (!SystemAPI.HasSingleton<PlayerPositionSingleton>()) return;
        float3 playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;

        foreach (var (transform, movement, sprite) in 
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyMovementData>, RefRW<IsometricSpriteData>>()
            .WithAll<EnemyTag>())
        {
            // Calcula direção do inimigo para o player
            float3 direction = playerPos - transform.ValueRO.Position;
            direction.y = 0; // Ignora Y
            
            if (math.lengthsq(direction) < 0.01f) continue;
            
            float3 dirNorm = math.normalize(direction);
            
            // Calcula dot products com cada direção (igual ao RobustIsometricController)
            float dotRight = math.dot(dirNorm, DirRight);
            float dotForward = math.dot(dirNorm, DirForward);
            float dotLeft = math.dot(dirNorm, DirLeft);
            float dotBack = math.dot(dirNorm, DirBack);

            // Encontra o maior dot product para determinar quadrante
            // Quadrantes: 0=DownRight, 1=DownLeft, 2=UpRight, 3=UpLeft
            int newDirection = 0;
            bool flipX = false;

            // Lógica baseada no RobustIsometricController original
            // DownRight: movimento para baixo-direita (dotRight > 0 && dotBack > 0)
            // DownLeft: movimento para baixo-esquerda (dotLeft > 0 && dotBack > 0)
            // UpRight: movimento para cima-direita (dotRight > 0 && dotForward > 0)
            // UpLeft: movimento para cima-esquerda (dotLeft > 0 && dotForward > 0)

            if (dotForward > 0) // Movendo para "cima" (Z+)
            {
                if (dotRight >= dotLeft)
                {
                    newDirection = 2; // UpRight
                    flipX = false;
                }
                else
                {
                    newDirection = 3; // UpLeft
                    flipX = true;
                }
            }
            else // Movendo para "baixo" (Z-)
            {
                if (dotRight >= dotLeft)
                {
                    newDirection = 0; // DownRight
                    flipX = false;
                }
                else
                {
                    newDirection = 1; // DownLeft
                    flipX = true;
                }
            }

            sprite.ValueRW.PreviousDirection = sprite.ValueRO.CurrentDirection;
            sprite.ValueRW.CurrentDirection = newDirection;
            sprite.ValueRW.FlipX = flipX;
        }
    }
}
