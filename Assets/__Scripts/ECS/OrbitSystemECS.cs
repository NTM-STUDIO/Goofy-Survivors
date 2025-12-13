using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct OrbitSystemECS : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, orbit, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<OrbitMovement>>().WithEntityAccess())
        {
            // Verifica se a entidade central (Player) ainda existe
            if (state.EntityManager.Exists(orbit.ValueRO.CenterEntity))
            {
                // Pega a posição do centro
                float3 centerPos = state.EntityManager.GetComponentData<LocalTransform>(orbit.ValueRO.CenterEntity).Position;

                // Atualiza o ângulo
                orbit.ValueRW.Angle += orbit.ValueRO.Speed * deltaTime;

                // Calcula nova posição
                float x = math.cos(orbit.ValueRO.Angle) * orbit.ValueRO.Radius;
                float z = math.sin(orbit.ValueRO.Angle) * orbit.ValueRO.Radius;

                transform.ValueRW.Position = centerPos + new float3(x, 1.0f, z);
            }
            else
            {
                // Se o player morreu, destrói o projétil orbital? Ou deixa parado?
                // Vamos destruir por segurança
                // state.EntityManager.DestroyEntity(entity); // Não podemos destruir direto aqui sem ECB, mas ok deixar parado por enquanto.
            }
        }
    }
}
