using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Data;
using TowerDefense.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Systems
{
    /// <summary>
    /// 流场烘焙系统 — 响应 FlowFieldBakeRequest，执行 BFS 生成流场
    ///
    /// 运行在 InitializationSystemGroup 中（GridSpawnSystem 之后），
    /// 确保地图数据就绪后再计算流场。
    ///
    /// 流程：
    /// 1. 检测 FlowFieldBakeRequest tag
    /// 2. 检查是否有预烘焙数据可用（可选持久化模式）
    /// 3. 调度 FlowFieldBakeJob (Burst)
    /// 4. 构建 BlobAsset 存储结果
    /// 5. 添加/更新 FlowFieldData 组件
    /// 6. 移除请求标记
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GridSpawnSystem))]
    public partial struct FlowFieldBakeSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("FlowFieldBakeSystem");
            state.RequireForUpdate<GridMapData>();
            state.RequireForUpdate<FlowFieldBakeRequest>();
            _logger.LogInformation("[FlowFieldBakeSystem] Created");
        }

        public void OnDestroy(ref SystemState state)
        {
            // 清理 BlobAsset
            if (SystemAPI.HasSingleton<FlowFieldData>())
            {
                var ffData = SystemAPI.GetSingleton<FlowFieldData>();
                if (ffData.BlobData.IsCreated)
                {
                    ffData.BlobData.Dispose();
                }
            }
            _logger.LogInformation("[FlowFieldBakeSystem] Destroyed");
        }

        public void OnUpdate(ref SystemState state)
        {
            var singletonEntity = SystemAPI.GetSingletonEntity<GridMapData>();
            var mapData = SystemAPI.GetSingleton<GridMapData>();

            // 读取目标点
            if (!state.EntityManager.HasBuffer<FlowFieldGoal>(singletonEntity))
            {
                _logger.LogWarning("[FlowFieldBakeSystem] No FlowFieldGoal buffer found, skipping bake");
                state.EntityManager.RemoveComponent<FlowFieldBakeRequest>(singletonEntity);
                return;
            }

            var goalBuffer = state.EntityManager.GetBuffer<FlowFieldGoal>(singletonEntity, true);
            int goalCount = goalBuffer.Length;
            if (goalCount == 0)
            {
                _logger.LogWarning("[FlowFieldBakeSystem] No goal points defined, skipping bake");
                state.EntityManager.RemoveComponent<FlowFieldBakeRequest>(singletonEntity);
                return;
            }

            // 立即将 buffer 数据拷贝到 NativeArray
            // 避免后续 EntityManager 调用（HasComponent / GetComponentData 等）
            // 使 DynamicBuffer 的 safety handle 失效，引发 ObjectDisposedException
            var goals = new NativeArray<int2>(goalCount, Allocator.TempJob);
            for (int i = 0; i < goalCount; i++)
                goals[i] = goalBuffer[i].GridCoord;
            // goalBuffer 在此之后不再使用

            // 尝试使用预烘焙数据
            if (TryUseBakedData(ref state, singletonEntity, mapData, goalCount))
            {
                goals.Dispose();
                state.EntityManager.RemoveComponent<FlowFieldBakeRequest>(singletonEntity);
                return;
            }

            int cellCount = mapData.Width * mapData.Height;


            // 从 BlobAsset 复制格子数据到可写 NativeArray
            ref var blobData = ref mapData.BlobData.Value;
            var cells = new NativeArray<byte>(cellCount, Allocator.TempJob);
            for (int i = 0; i < cellCount; i++)
                cells[i] = blobData.Cells[i];

            // 分配输出和工作缓冲区
            var directions = new NativeArray<byte>(cellCount, Allocator.TempJob);
            var costField = new NativeArray<int>(cellCount, Allocator.TempJob);
            var queue = new NativeQueue<int>(Allocator.TempJob);

            // 执行烘焙
            var handle = FlowFieldBaker.ScheduleBake(
                cells, mapData.Width, mapData.Height,
                goals, directions, costField, queue);
            handle.Complete();

            // 构建 BlobAsset
            var blobRef = BuildFlowFieldBlob(directions, cellCount);

            // 清理旧的流场数据
            if (state.EntityManager.HasComponent<FlowFieldData>(singletonEntity))
            {
                var oldData = state.EntityManager.GetComponentData<FlowFieldData>(singletonEntity);
                if (oldData.BlobData.IsCreated)
                    oldData.BlobData.Dispose();
            }

            // 计算数据哈希用于校验
            uint dataHash = ComputeDataHash(cells, goals);

            // 设置组件（安全模式：先确保组件存在，再设值）
            if (!state.EntityManager.HasComponent<FlowFieldData>(singletonEntity))
                state.EntityManager.AddComponent<FlowFieldData>(singletonEntity);
            state.EntityManager.SetComponentData(singletonEntity, new FlowFieldData
            {
                BlobData = blobRef,
                DataHash = dataHash,
                AlgorithmVersion = LevelConfig.FlowFieldAlgorithmVersion,
                GoalCount = goalCount
            });

            _logger.LogInformation(
                "[FlowFieldBakeSystem] Flow field baked: {0}x{1}, {2} goals",
                mapData.Width, mapData.Height, goalCount);

            // 清理
            goals.Dispose();
            cells.Dispose();
            directions.Dispose();
            costField.Dispose();
            queue.Dispose();

            // 移除请求
            state.EntityManager.RemoveComponent<FlowFieldBakeRequest>(singletonEntity);
        }

        private bool TryUseBakedData(ref SystemState state, Entity entity,
            GridMapData mapData, int goalCount)
        {
            // 检查 SharedLevelDataStore 中是否有预烘焙数据
            // 这里通过检查已有的 FlowFieldData 组件版本来判断
            // 预烘焙数据在 GridSpawnSystem 中加载时已经写入
            if (!state.EntityManager.HasComponent<FlowFieldData>(entity))
                return false;

            var existing = state.EntityManager.GetComponentData<FlowFieldData>(entity);
            if (!existing.BlobData.IsCreated)
                return false;
            if (existing.AlgorithmVersion != LevelConfig.FlowFieldAlgorithmVersion)
                return false;
            if (existing.GoalCount != goalCount)
                return false;

            _logger.LogInformation("[FlowFieldBakeSystem] Using pre-baked flow field data");
            return true;
        }

        private BlobAssetReference<FlowFieldBlobData> BuildFlowFieldBlob(
            NativeArray<byte> directions, int cellCount)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FlowFieldBlobData>();

            var dirArray = builder.Allocate(ref root.Directions, cellCount);
            for (int i = 0; i < cellCount; i++)
                dirArray[i] = directions[i];

            return builder.CreateBlobAssetReference<FlowFieldBlobData>(Allocator.Persistent);
        }

        private static uint ComputeDataHash(NativeArray<byte> cells, NativeArray<int2> goals)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < cells.Length; i++)
            {
                hash ^= cells[i];
                hash *= 16777619u;
            }
            for (int i = 0; i < goals.Length; i++)
            {
                hash ^= (uint)goals[i].x;
                hash *= 16777619u;
                hash ^= (uint)goals[i].y;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
