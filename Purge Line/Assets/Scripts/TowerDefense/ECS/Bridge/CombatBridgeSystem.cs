using Base.BaseSystem.EventSystem;
using Microsoft.Extensions.Logging;
using PurgeLine.Events;
using TowerDefense.Components;
using TowerDefense.ECS;
using TowerDefense.Data;
using TowerDefense.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityDependencyInjection;

namespace TowerDefense.Bridge
{
    /// <summary>
    /// 战斗桥接系统 — Managed 世界 ↔ ECS 战斗系统
    ///
    /// 职责：
    /// 1. 监听地图加载完成事件，初始化 ECS 战斗 singleton 数据
    /// 2. 创建 EnemySpawnTimer singleton + SpawnPointData buffer
    /// 3. 提供炮塔创建 API（供 TowerPlacementSystem 调用）
    /// 4. 管理战斗全局状态
    ///
    /// 注册到 DependencyManager，在 GridBridgeSystem 之后初始化。
    /// </summary>
    public class CombatBridgeSystem : IInitializable, IStartable
    {
        private static ILogger _logger;
        private World _ecsWorld;
        private System.IDisposable _mapLoadedSub;

        // 战斗是否已初始化
        public bool IsCombatReady { get; private set; }

        // ── IInitializable ────────────────────────────────────

        public void OnInit()
        {
            _logger = GameLogger.Create("CombatBridgeSystem");
            _logger.LogInformation("[CombatBridgeSystem] Initialized");
        }

        public void OnStart()
        {
            _ecsWorld = World.DefaultGameObjectInjectionWorld;
            if (_ecsWorld == null)
            {
                _logger.LogError("[CombatBridgeSystem] Default ECS World is null!");
                return;
            }

            // 订阅地图加载完成事件
            _mapLoadedSub = EventManager.Gameplay.AddListener<GridMapLoadedEvent>(OnMapLoaded);
            _logger.LogInformation("[CombatBridgeSystem] Started, waiting for map load");
        }

        public void OnDispose()
        {
            _mapLoadedSub?.Dispose();
            CleanupCombatEntities();
            _logger.LogInformation("[CombatBridgeSystem] Disposed");
        }

        // ── 地图加载回调 ─────────────────────────────────────

        private void OnMapLoaded(GridMapLoadedEvent evt)
        {
            _logger.LogInformation("[CombatBridgeSystem] Map loaded: {0} ({1}x{2}), initializing combat",
                evt.LevelId, evt.Width, evt.Height);

            InitializeCombatSingletons(evt);
        }

        /// <summary>
        /// 初始化战斗 ECS singleton 数据
        /// </summary>
        private void InitializeCombatSingletons(GridMapLoadedEvent evt)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return;

            var em = _ecsWorld.EntityManager;

            // 清理旧数据
            CleanupCombatEntities();

            // ── 创建 EnemySpawnTimer singleton ──────────────────
            // 注意：使用 EndSimulationEntityCommandBufferSystem 因为此方法可能在 ECS 系统迭代期间被调用
            // 使用 EndSimulationECB 让变更在当前帧结束时自动应用，而不是立即执行

            var ecbSystem = _ecsWorld.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            var spawnTimerEntity = ecb.CreateEntity();
            ecb.AddComponent(spawnTimerEntity, new EnemySpawnTimer
            {
                SpawnInterval = CombatConfig.EnemySpawnInterval,
                Timer = 0f,
                SpawnedCount = 0,
                MaxSpawnCount = CombatConfig.EnemyMaxSpawnCount,
                BatchSize = CombatConfig.EnemySpawnBatchSize
            });

            // 添加出生点 buffer
            var spawnBuffer = ecb.AddBuffer<SpawnPointData>(spawnTimerEntity);

            // 从 GridMapData 获取地图信息，从 LevelConfig 获取出生点
            using var mapQuery = em.CreateEntityQuery(ComponentType.ReadOnly<GridMapData>());
            if (!mapQuery.IsEmpty)
            {
                var mapData = mapQuery.GetSingleton<GridMapData>();

                // 从关卡配置获取出生点
                var bridgeSystem = DependencyManager.Instance.Get<GridBridgeSystem>();
                if (bridgeSystem != null && bridgeSystem.CurrentLevelId != null)
                {
                    var levelConfig = LevelConfigLoader.LoadFromResources(bridgeSystem.CurrentLevelId);
                    if (levelConfig?.SpawnPoints != null)
                    {
                        foreach (var sp in levelConfig.SpawnPoints)
                        {
                            int gx = (int)sp.x;
                            int gy = (int)sp.y;
                            // 转换为世界坐标
                            GridMath.GridToWorld(
                                new int2(gx, gy), mapData.Origin, mapData.CellSize, out float2 worldPos);

                            spawnBuffer.Add(new SpawnPointData { WorldPosition = worldPos });
                            _logger.LogInformation("[CombatBridgeSystem] Spawn point: grid({0},{1}) → world({2},{3})",
                                gx, gy, worldPos.x, worldPos.y);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[CombatBridgeSystem] No spawn points in level config, using default");
                        AddDefaultSpawnPoints(spawnBuffer, mapData);
                    }
                }
                else
                {
                    _logger.LogWarning("[CombatBridgeSystem] GridBridgeSystem not ready, using default spawn points");
                    AddDefaultSpawnPoints(spawnBuffer, mapData);
                }
            }
            else
            {
                _logger.LogWarning("[CombatBridgeSystem] GridMapData not found, deferring spawn point setup");
            }

            // 注意：使用 EndSimulationEntityCommandBufferSystem 时不需要手动调用 Playback
            // ECB 会在当前帧 Simulation 阶段结束时自动应用所有变更

            IsCombatReady = true;
            _logger.LogInformation("[CombatBridgeSystem] Combat initialized, {0} spawn points",
                spawnBuffer.Length);
        }

        /// <summary>
        /// 添加默认出生点（地图左侧中间）
        /// </summary>
        private void AddDefaultSpawnPoints(DynamicBuffer<SpawnPointData> buffer, GridMapData mapData)
        {
            // 默认在地图左侧中间位置添加出生点
            int midY = mapData.Height / 2;
            GridMath.GridToWorld(new int2(0, midY), mapData.Origin, mapData.CellSize, out float2 pos);
            buffer.Add(new SpawnPointData { WorldPosition = pos });
        }

        // ── 炮塔创建 API ─────────────────────────────────────

        /// <summary>
        /// 在指定格子创建炮塔实体
        /// </summary>
        /// <param name="gridCoord">格子坐标</param>
        /// <returns>创建的炮塔 Entity，失败返回 Entity.Null</returns>
        public Entity CreateTower(int2 gridCoord)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return Entity.Null;

            var em = _ecsWorld.EntityManager;
            var bridgeSystem = DependencyManager.Instance.Get<GridBridgeSystem>();

            // 检查是否可以放置
            if (!bridgeSystem.CanPlaceAt(gridCoord))
            {
                _logger.LogWarning("[CombatBridgeSystem] Cannot place tower at ({0},{1})",
                    gridCoord.x, gridCoord.y);
                return Entity.Null;
            }

            // 获取世界坐标
            float2 worldPos = bridgeSystem.GridToWorld(gridCoord);

            // 创建炮塔实体
            var towerEntity = em.CreateEntity();

            em.AddComponentData(towerEntity, LocalTransform.FromPosition(
                new float3(worldPos.x, worldPos.y, 0f)));
            em.AddComponent<TowerTag>(towerEntity);
            em.AddComponentData(towerEntity, new TowerData
            {
                AttackRange = CombatConfig.DefaultTowerRange,
                AttackInterval = CombatConfig.DefaultTowerInterval,
                BulletSpeed = CombatConfig.DefaultBulletSpeed,
                Damage = CombatConfig.DefaultTowerDamage,
                Level = 1,
                TowerTypeId = 0
            });
            em.AddComponentData(towerEntity, new TowerState
            {
                AttackTimer = 0f,
                CurrentTarget = Entity.Null
            });
            em.AddComponentData(towerEntity, new GridCoord(gridCoord));

            // 可视化请求 — EcsVisualBridge 检测后实例化 Tower prefab
            em.AddComponentData(towerEntity, new VisualPrefab
            {
                PrefabAddress = CombatConfig.TowerPrefabAddress
            });

            // 标记格子为已占据
            bridgeSystem.PlaceTower(gridCoord, towerEntity);

            // 放置炮塔后触发流场重新烘焙（因为格子被占据可能影响寻路）
            // 注意：当前设计中炮塔放置在 Placeable 格子上，不影响 Walkable 路径
            // 如果后续设计中炮塔会阻挡路径，则需要 RebakeFlowField
            // bridgeSystem.RebakeFlowField();

            _logger.LogInformation("[CombatBridgeSystem] Tower created at ({0},{1}), Entity={2}",
                gridCoord.x, gridCoord.y, towerEntity.Index);

            // 派发事件
            EventManager.Gameplay.Dispatch(new TowerPlacedEvent
            {
                GridCoord = gridCoord,
                TowerEntity = towerEntity,
                TowerTypeId = 0
            });

            return towerEntity;
        }

        // ── 清理 ─────────────────────────────────────────────

        private void CleanupCombatEntities()
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return;

            var em = _ecsWorld.EntityManager;

            // 清理 EnemySpawnTimer singleton
            using var timerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawnTimer>());
            if (!timerQuery.IsEmpty)
            {
                em.DestroyEntity(timerQuery);
            }

            IsCombatReady = false;
        }
    }
}

