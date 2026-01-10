using Unity.Entities;
using UnityEngine;

public partial class XpBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<XpToProcessData>()) return;
        
        RefRW<XpToProcessData> xpData = SystemAPI.GetSingletonRW<XpToProcessData>();
        
        if (xpData.ValueRO.Amount > 0)
        {
            if (PlayerExperience.Instance != null)
            {
                PlayerExperience.Instance.AddGlobalXP(xpData.ValueRO.Amount);
            }
            
            xpData.ValueRW.Amount = 0;
        }
    }
}
