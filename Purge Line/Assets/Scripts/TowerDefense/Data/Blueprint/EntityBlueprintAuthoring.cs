using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TowerDefense.Data.Blueprint
{
    public sealed class EntityBlueprintAuthoring : MonoBehaviour
    {
        [SerializeField] private string blueprintPath;

        public string BlueprintPath
        {
            get => blueprintPath;
            set => blueprintPath = value;
        }

        private sealed class EntityBlueprintAuthoringBaker : Unity.Entities.Baker<EntityBlueprintAuthoring>
        {
            public override void Bake(EntityBlueprintAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EntityBlueprintReference
                {
                    blueprintPath = new FixedString512Bytes(authoring.blueprintPath ?? string.Empty)
                });
            }
        }
    }

    public struct EntityBlueprintReference : IComponentData
    {
        public FixedString512Bytes blueprintPath;
    }
}


