using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DamageVisualsConsumerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<DamageVisualsSingleton>()) return;

        Entity singletonEntity = SystemAPI.GetSingletonEntity<DamageVisualsSingleton>();
        DynamicBuffer<DamageVisualEvent> buffer = SystemAPI.GetBuffer<DamageVisualEvent>(singletonEntity);

        if (buffer.IsEmpty) return;

        // Verifica se o Manager existe (MonoBehaviour)
        if (DamageVisualsManager.Instance != null)
        {
            foreach (var evt in buffer)
            {
                DamageVisualsManager.Instance.SpawnPopup(evt.Position, evt.Amount, evt.IsCritical);
            }
        }

        buffer.Clear();
    }
}
