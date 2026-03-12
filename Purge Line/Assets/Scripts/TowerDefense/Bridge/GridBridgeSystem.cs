using Base.BaseSystem.EventSystem;
using Microsoft.Extensions.Logging;
using PurgeLine.Events;
using R3;
using TowerDefense.Components;
using TowerDefense.Data;
using TowerDefense.Systems;
using TowerDefense.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnitySystemArchitecture.Core;
using ISystem = UnitySystemArchitecture.Core.ISystem;

namespace TowerDefense.Bridge
{
    /// <summary>
    /// 网格桥接系统 — Managed 世界 ↔ ECS 世界
    ///
    /// 注册到 SystemManager 的托管系统，提供：
    /// 1. 关卡加载触发（创建 GridSpawnRequest ECS entity）
    /// 2. R3 响应式事件流（MapLoaded, CellChanged, FlowFieldBaked）
    /// 3. 面向业务层的查询 API
    /// 4. 流场管理（触发重新烘焙等）
    ///
    /// 生命周期：
    ///   GameFramework.Initialize() → SystemManager.Register<GridBridgeSystem>()
    ///   → OnInit() → OnStart() → LoadLevel() → ...
    /// </summary>
    public class GridBridgeSystem : ISystem, IStart
    {
        private static ILogger _logger;

        // ── ECS World 引用 ────────────────────────────────────

        private World _ecsWorld;

        // ── 状态 ──────────────────────────────────────────────

        /// <summary>当前加载的关卡ID</summary>
        public string CurrentLevelId { get; private set; }

        /// <summary>是否已加载地图</summary>
        public bool IsMapLoaded { get; private set; }

        /// <summary>流场是否已就绪</summary>
        public bool IsFlowFieldReady
        {
            get
            {
                if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;
                var em = _ecsWorld.EntityManager;
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldData>());
                if (query.IsEmpty) return false;
                var ffData = query.GetSingleton<FlowFieldData>();
                return ffData.BlobData.IsCreated;
            }
        }

        // ── ISystem 生命周期 ──────────────────────────────────

        public void OnInit()
        {
            _logger = GameLogger.Create("GridBridgeSystem");
            _logger.LogInformation("[GridBridgeSystem] Initialized");
        }

        public void OnStart()
        {
            _ecsWorld = World.DefaultGameObjectInjectionWorld;
            if (_ecsWorld == null)
            {
                _logger.LogError("[GridBridgeSystem] Default ECS World is null!");
                return;
            }

            _logger.LogInformation("[GridBridgeSystem] Started, ECS World ready");
        }

        public void OnDispose()
        {
            SharedLevelDataStore.Clear();
            _logger.LogInformation("[GridBridgeSystem] Disposed");
        }

        // ── 关卡加载 ─────────────────────────────────────────

        /// <summary>
        /// 从 Resources 加载关卡并生成地图
        /// </summary>
        /// <param name="levelId">关卡ID</param>
        /// <returns>是否成功发起加载</returns>
        public bool LoadLevel(string levelId)
        {
            var config = LevelConfigLoader.LoadFromResources(levelId);
            if (config == null)
            {
                _logger.LogError("[GridBridgeSystem] Failed to load level: {0}", levelId);
                return false;
            }

            return LoadLevelFromConfig(config);
        }

        /// <summary>
        /// 从文件路径加载关卡
        /// </summary>
        public bool LoadLevelFromFile(string filePath)
        {
            var config = LevelConfigLoader.LoadFromFile(filePath);
            if (config == null)
            {
                _logger.LogError("[GridBridgeSystem] Failed to load level from file: {0}", filePath);
                return false;
            }

            return LoadLevelFromConfig(config);
        }

        /// <summary>
        /// 从 LevelConfig 直接生成地图
        /// </summary>
        public bool LoadLevelFromConfig(LevelConfig config)
        {
            if (config == null)
            {
                _logger.LogError("[GridBridgeSystem] Config is null");
                return false;
            }

            if (!config.Validate(out var error))
            {
                _logger.LogError("[GridBridgeSystem] Config validation failed: {0}", error);
                return false;
            }

            if (_ecsWorld == null || !_ecsWorld.IsCreated)
            {
                _logger.LogError("[GridBridgeSystem] ECS World is not available");
                return false;
            }

            // 将配置存入共享仓库
            int dataId = SharedLevelDataStore.Store(config);

            // 创建 ECS 请求实体
            var entityManager = _ecsWorld.EntityManager;
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new GridSpawnRequest
            {
                Width = config.Width,
                Height = config.Height,
                CellSize = config.CellSize,
                OriginX = config.OriginX,
                OriginY = config.OriginY,
                CellDataId = dataId
            });

            CurrentLevelId = config.LevelId;
            IsMapLoaded = true;

            _logger.LogInformation("[GridBridgeSystem] Level load requested: {0} ({1}x{2}), Goals={3}",
                config.LevelId, config.Width, config.Height, config.GoalPoints?.Length ?? 0);
            
            GameEventSystem.Gameplay.Dispatch(new GridMapLoadedEvent{
                    LevelId = config.LevelId,
                    Width = config.Width,
                    Height = config.Height,
                    CellSize = config.CellSize
                });

            return true;
        }

        // ── 流场管理 ─────────────────────────────────────────

        /// <summary>
        /// 触发流场重新烘焙（例如障碍物变化后调用）
        /// </summary>
        public bool RebakeFlowField()
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;

            var em = _ecsWorld.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GridMapData>());
            if (query.IsEmpty)
            {
                _logger.LogWarning("[GridBridgeSystem] Cannot rebake: no GridMapData");
                return false;
            }

            var entity = query.GetSingletonEntity();
            if (!em.HasComponent<FlowFieldBakeRequest>(entity))
            {
                em.AddComponent<FlowFieldBakeRequest>(entity);
            }

            _logger.LogInformation("[GridBridgeSystem] Flow field rebake requested");
            return true;
        }

        // ── 查询 API ─────────────────────────────────────────

        /// <summary>
        /// 查询指定世界坐标的格子信息
        /// </summary>
        public bool TryGetCellAt(float2 worldPos, out int2 gridCoord,
            out CellType cellType, out bool isOccupied)
        {
            gridCoord = default;
            cellType = CellType.Solid;
            isOccupied = false;

            if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;

            bool found = GridModificationSystem.TryGetCellAtWorldPos(
                _ecsWorld.EntityManager, worldPos, out gridCoord, out cellType, out var cellState);

            if (found)
                isOccupied = cellState.IsOccupied;

            return found;
        }

        /// <summary>
        /// 在指定格子放置炮塔
        /// </summary>
        public bool PlaceTower(int2 gridCoord, Entity towerEntity)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;

            bool success = GridModificationSystem.TryPlaceTower(
                _ecsWorld.EntityManager, gridCoord, towerEntity);

            if (success)
            {
                GameEventSystem.Gameplay.Dispatch(new GridCellChangedEvent
                {
                    GridCoord = gridCoord,
                    ChangeType = CellChangeType.TowerPlaced,
                    Entity = towerEntity
                });
            }

            return success;
        }

        /// <summary>
        /// 从指定格子移除炮塔
        /// </summary>
        public bool RemoveTower(int2 gridCoord)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;

            bool success = GridModificationSystem.TryRemoveTower(
                _ecsWorld.EntityManager, gridCoord);

            if (success)
            {
                GameEventSystem.Gameplay.Dispatch(new GridCellChangedEvent
                {
                    GridCoord = gridCoord,
                    ChangeType = CellChangeType.TowerRemoved,
                    Entity = Entity.Null
                });
            }

            return success;
        }

        /// <summary>
        /// 检查是否可以在指定位置放置
        /// </summary>
        public bool CanPlaceAt(int2 gridCoord)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return false;
            return GridModificationSystem.CanPlaceAt(_ecsWorld.EntityManager, gridCoord);
        }

        /// <summary>
        /// 世界坐标转格子坐标
        /// </summary>
        public int2 WorldToGrid(float2 worldPos)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return default;

            var entityManager = _ecsWorld.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapData>());
            if (query.IsEmpty) return default;

            var mapData = query.GetSingleton<GridMapData>();
            GridMath.WorldToGrid(worldPos, mapData.Origin, mapData.CellSize, out int2 result);
            return result;
        }

        /// <summary>
        /// 格子坐标转世界坐标（中心点）
        /// </summary>
        public float2 GridToWorld(int2 gridCoord)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return default;

            var entityManager = _ecsWorld.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapData>());
            if (query.IsEmpty) return default;

            var mapData = query.GetSingleton<GridMapData>();
            GridMath.GridToWorld(gridCoord, mapData.Origin, mapData.CellSize, out float2 result);
            return result;
        }
    }
}

