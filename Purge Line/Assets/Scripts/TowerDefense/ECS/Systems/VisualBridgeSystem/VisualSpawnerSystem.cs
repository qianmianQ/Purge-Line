using TowerDefense.ECS.Bridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using VContainer;

namespace TowerDefense.ECS
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VisualSpawnerSystem : ISystem
    {
        // 限制单帧生成数，避免首帧或波次切换时主线程尖峰。
        private const int MaxSpawnsPerFrame = 64;

        private EntityQuery _spawnQuery;

        public void OnCreate(ref SystemState state)
        {
            _spawnQuery = SystemAPI.QueryBuilder()
                .WithAll<VisualPrefab>()
                .WithNone<VisualLink>()
                .Build();

            state.RequireForUpdate(_spawnQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            var scope = GameLifetimeScope.Instance;
            var bridge = scope?.Container?.Resolve<IEcsVisualBridgeSystem>();
            if (bridge == null) return;

            var entityManager = state.EntityManager;
            using var entities = _spawnQuery.ToEntityArray(Allocator.Temp);

            int spawnCount = entities.Length < MaxSpawnsPerFrame ? entities.Length : MaxSpawnsPerFrame;

            for (int i = 0; i < spawnCount; i++)
            {
                var entity = entities[i];
                if (!entityManager.Exists(entity) || entityManager.HasComponent<VisualLink>(entity))
                    continue;

                var prefab = entityManager.GetComponentData<VisualPrefab>(entity);
                var prefabAddress = prefab.PrefabAddress.ToString();
                var go = bridge.GetGameObjectInPoolSync(prefabAddress);

                // 对象池暂时不可用时保留 VisualPrefab，下一帧自动重试。
                if (go == null)
                    continue;

                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                    go.transform.SetPositionAndRotation(localTransform.Position, localTransform.Rotation);
                }

                go.SetActive(true);
                entityManager.AddComponentData(entity, new VisualLink
                {
                    GameObjectRef = go,
                    PrefabAddress = prefabAddress
                });
                entityManager.RemoveComponent<VisualPrefab>(entity);
            }
        }
    }
}
