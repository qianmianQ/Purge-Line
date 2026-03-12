using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
    /// 6. 创建 FlowFieldGoal buffer 并触发流场烘焙
    /// 7. 添加 GridDirtyTag 通知渲染刷新
    /// 8. 销毁 request entity
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
            // 清理 GridMapData BlobAsset（FlowFieldData 由 FlowFieldBakeSystem 负责清理）
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

            // 必须在 Query 迭代之前清理旧 singleton：
            // EntityManager.DestroyEntity 是结构性变更，在 foreach 内部调用会抛出
            // InvalidOperationException，同时导致 ECB 未 Playback，
            // 请求实体残留，下一帧 Consume 时数据已不存在（LevelConfig not found）。
            CleanupExistingSingleton(ref state);

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


                // 创建 singleton entity
                var singletonEntity = ecb.CreateEntity();
                ecb.AddComponent(singletonEntity, new GridMapData
                {
                    Width = levelConfig.Width,
                    Height = levelConfig.Height,
                    CellSize = levelConfig.CellSize,
                    Origin = new float2(levelConfig.OriginX, levelConfig.OriginY),
                    BlobData = blobRef
                });

                // 添加 GridCellState buffer
                var cellStateBuffer = ecb.AddBuffer<GridCellState>(singletonEntity);
                for (int i = 0; i < levelConfig.CellCount; i++)
                {
                    cellStateBuffer.Add(GridCellState.Empty);
                }

                // 创建 FlowFieldGoal buffer
                var goalBuffer = ecb.AddBuffer<FlowFieldGoal>(singletonEntity);
                if (levelConfig.GoalPoints != null)
                {
                    for (int i = 0; i < levelConfig.GoalPoints.Length; i++)
                    {
                        var gp = levelConfig.GoalPoints[i];
                        int gx = Mathf.FloorToInt(gp.x);
                        int gy = Mathf.FloorToInt(gp.y);
                        if (gx >= 0 && gx < levelConfig.Width && gy >= 0 && gy < levelConfig.Height)
                        {
                            goalBuffer.Add(new FlowFieldGoal { GridCoord = new int2(gx, gy) });
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[GridSpawnSystem] Goal point ({0},{1}) is out of bounds, skipped",
                                gp.x, gp.y);
                        }
                    }
                }

                // 尝试加载预烘焙流场数据
                if (levelConfig.HasValidBakedFlowField())
                {
                    var ffBlobRef = BuildFlowFieldBlob(levelConfig.BakedFlowFieldDirections);
                    ecb.AddComponent(singletonEntity, new FlowFieldData
                    {
                        BlobData = ffBlobRef,
                        DataHash = levelConfig.BakedFlowFieldDataHash,
                        AlgorithmVersion = levelConfig.BakedFlowFieldVersion,
                        GoalCount = levelConfig.GoalPoints?.Length ?? 0
                    });
                    _logger.LogInformation("[GridSpawnSystem] Loaded pre-baked flow field data");
                }

                // 添加流场烘焙请求（如果有目标点）
                if (goalBuffer.Length > 0)
                {
                    ecb.AddComponent<FlowFieldBakeRequest>(singletonEntity);
                }
                else
                {
                    _logger.LogWarning("[GridSpawnSystem] No valid goal points, flow field bake skipped");
                }

                // 标记脏，通知渲染系统
                ecb.AddComponent<GridDirtyTag>(singletonEntity);

                _logger.LogInformation("[GridSpawnSystem] Map created: {0}x{1}, CellSize={2}, Origin=({3},{4}), Goals={5}",
                    levelConfig.Width, levelConfig.Height, levelConfig.CellSize,
                    levelConfig.OriginX, levelConfig.OriginY, goalBuffer.Length);

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
        /// 从预烘焙方向数据构建流场 BlobAsset
        /// </summary>
        private BlobAssetReference<FlowFieldBlobData> BuildFlowFieldBlob(byte[] directions)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FlowFieldBlobData>();

            var dirArray = builder.Allocate(ref root.Directions, directions.Length);
            for (int i = 0; i < directions.Length; i++)
            {
                dirArray[i] = directions[i];
            }

            return builder.CreateBlobAssetReference<FlowFieldBlobData>(Allocator.Persistent);
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

            // 清理流场 BlobAsset
            if (SystemAPI.HasSingleton<FlowFieldData>())
            {
                var ffData = SystemAPI.GetSingleton<FlowFieldData>();
                if (ffData.BlobData.IsCreated)
                {
                    ffData.BlobData.Dispose();
                }
            }

            var existingEntity = SystemAPI.GetSingletonEntity<GridMapData>();
            state.EntityManager.DestroyEntity(existingEntity);

            _logger.LogInformation("[GridSpawnSystem] Cleaned up existing map singleton");
        }
    }
}

