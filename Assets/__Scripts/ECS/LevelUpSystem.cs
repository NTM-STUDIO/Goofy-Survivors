using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct LevelUpSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (stats, entity) in SystemAPI.Query<RefRW<PlayerStatsData>>().WithEntityAccess())
        {
            // Verifica se tem XP suficiente para subir de nível
            if (stats.ValueRO.CurrentXP >= stats.ValueRO.MaxXP)
            {
                // 1. Consome o XP (mantendo o excedente)
                stats.ValueRW.CurrentXP -= stats.ValueRO.MaxXP;
                
                // 2. Sobe o Nível
                stats.ValueRW.Level++;
                
                // 3. Aumenta a exigência para o próximo nível (Curva de XP)
                // Exemplo: Aumenta 20% a cada nível
                stats.ValueRW.MaxXP *= 1.2f;

                // 4. Cura o player ao subir de nível (Bônus clássico)
                stats.ValueRW.CurrentHealth = stats.ValueRO.MaxHealth;

                // Nota: Aqui seria o lugar onde pausaríamos o jogo para abrir o menu de Upgrades.
                // Como ainda não temos o Menu ECS, ele vai apenas subir de nível automaticamente e ficar mais forte.
                UnityEngine.Debug.Log($"LEVEL UP! Novo Nível: {stats.ValueRW.Level}");
            }
        }
    }
}
