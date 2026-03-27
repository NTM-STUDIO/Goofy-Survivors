using Unity.Entities;
using UnityEngine;

namespace GoofySurvivors.ECS {
    public class DecorationAuthoring : MonoBehaviour {
        public GameObject[] decorationPrefabs;

        class Baker : Baker<DecorationAuthoring> {
            public override void Bake(DecorationAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                
                var buffer = AddBuffer<DecorationPrefabElement>(entity);
                foreach (var go in authoring.decorationPrefabs) {
                    buffer.Add(new DecorationPrefabElement {
                        Prefab = GetEntity(go, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}