using Unity.Entities;
using Unity.Transforms;

namespace TowerDefense.ECS
{
    [UpdateInGroup(typeof(PresentationSystemGroup))] // 放在 Presentation 级别，逻辑计算完成后同步
    public partial struct VisualTransformSyncSystem : ISystem
    {
        private EntityQuery _syncQuery;

        public void OnCreate(ref SystemState state)
        {
            _syncQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, VisualLink>()
                .Build();

            state.RequireForUpdate(_syncQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (localTransform, visualLink) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<VisualLink>>())
            {
                var go = visualLink.ValueRO.GameObjectRef.Value;
                if (go == null) continue;

                go.transform.SetPositionAndRotation(localTransform.ValueRO.Position, localTransform.ValueRO.Rotation);
            }
        }
    }
}