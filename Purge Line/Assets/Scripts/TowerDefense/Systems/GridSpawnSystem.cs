using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TowerDefense.Systems
{
    /// <summary>
    /// 网格生成系统 — 消费 GridSpawnRequest，构建地图 BlobAsset
    ///
    /// 运行在 InitializationSystemGroup 中，确保地图数据在其他系统更新前就绪。
    ///
    /// 流程：
    /// 1. 检测到 GridSpawnRequest entity
    /// 2. 从 SharedLevelDataStore 获取 LevelConfig
    /// 3. 构建 BlobAsset<GridBlobData>
    /// 4. 创建/更新 GridMapData singleton
    /// 5. 初始化 GridCellState buffer
    /// 6. 添加 GridDirtyTag 通知渲染刷新
    /// 7. 销毁 request entity
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GridSpawnSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("GridSpawnSystem");
            state.RequireForUpdate<GridSpawnRequest>();
            _logger.LogInformation("[GridSpawnSystem] Created");
        }

        public void OnDestroy(ref SystemState state)
        {
            // 清理 BlobAsset
            if (SystemAPI.HasSingleton<GridMapData>())
            {
                var mapData = SystemAPI.GetSingleton<GridMapData>();
                if (mapData.BlobData.IsCreated)
                {
                    mapData.BlobData.Dispose();
                }
            }

            _logger.LogInformation("[GridSpawnSystem] Destroyed");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in
                     SystemAPI.Query<RefRO<GridSpawnRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                _logger.LogInformation("[GridSpawnSystem] Processing spawn request: {0}x{1}, CellDataId={2}",
                    req.Width, req.Height, req.CellDataId);

                // 从共享存储获取配置
                var levelConfig = SharedLevelDataStore.Consume(req.CellDataId);
                if (levelConfig == null)
                {
                    _logger.LogError("[GridSpawnSystem] LevelConfig not found for CellDataId={0}", req.CellDataId);
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // 构建 BlobAsset
                var blobRef = BuildBlobAsset(levelConfig);

                // 清理旧的 singleton（如果存在）
                CleanupExistingSingleton(ref state);

                // 创建 singleton entity
                var singletonEntity = ecb.CreateEntity();
                ecb.AddComponent(singletonEntity, new GridMapData
                {
                    Width = levelConfig.Width,
                    Height = levelConfig.Height,
                    CellSize = levelConfig.CellSize,
                    Origin = new Unity.Mathematics.float2(levelConfig.OriginX, levelConfig.OriginY),
                    BlobData = blobRef
                });

                // 添加 GridCellState buffer
                var buffer = ecb.AddBuffer<GridCellState>(singletonEntity);
                for (int i = 0; i < levelConfig.CellCount; i++)
                {
                    buffer.Add(GridCellState.Empty);
                }

                // 标记脏，通知渲染系统
                ecb.AddComponent<GridDirtyTag>(singletonEntity);

                _logger.LogInformation("[GridSpawnSystem] Map created: {0}x{1}, CellSize={2}, Origin=({3},{4})",
                    levelConfig.Width, levelConfig.Height, levelConfig.CellSize,
                    levelConfig.OriginX, levelConfig.OriginY);

                // 销毁请求
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// 从 LevelConfig 构建 BlobAsset
        /// </summary>
        private BlobAssetReference<GridBlobData> BuildBlobAsset(LevelConfig config)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GridBlobData>();

            var cellArray = builder.Allocate(ref root.Cells, config.CellCount);
            for (int i = 0; i < config.CellCount; i++)
            {
                cellArray[i] = config.Cells[i];
            }

            var blobRef = builder.CreateBlobAssetReference<GridBlobData>(Allocator.Persistent);
            _logger.LogDebug("[GridSpawnSystem] BlobAsset built: {0} cells", config.CellCount);
            return blobRef;
        }

        /// <summary>
        /// 清理已存在的 GridMapData singleton
        /// </summary>
        private void CleanupExistingSingleton(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<GridMapData>()) return;

            var existingData = SystemAPI.GetSingleton<GridMapData>();
            if (existingData.BlobData.IsCreated)
            {
                existingData.BlobData.Dispose();
            }

            var existingEntity = SystemAPI.GetSingletonEntity<GridMapData>();
            state.EntityManager.DestroyEntity(existingEntity);

            _logger.LogInformation("[GridSpawnSystem] Cleaned up existing map singleton");
        }
    }
}

