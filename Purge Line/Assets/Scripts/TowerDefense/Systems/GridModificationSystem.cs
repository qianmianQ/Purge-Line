using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Systems
{
    /// <summary>
    /// 网格修改系统 — 处理格子状态变更
    ///
    /// 职责：
    /// - 炮塔放置：标记格子为已占据，记录占据者 Entity
    /// - 炮塔移除：释放格子占据状态
    /// - 格子状态查询 API（供其他系统调用）
    ///
    /// 运行在 SimulationSystemGroup 中。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GridModificationSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create<GridModificationSystem>();
            state.RequireForUpdate<GridMapData>();
            _logger.LogInformation("[GridModificationSystem] Created");
        }

        public void OnDestroy(ref SystemState state)
        {
            _logger.LogInformation("[GridModificationSystem] Destroyed");
        }

        public void OnUpdate(ref SystemState state)
        {
            // 本系统当前为被动系统：仅在有修改请求时被其他系统调用 API
            // 后续可扩展为处理 GridModificationRequest buffer
        }

        // ── 公开 API（主线程调用）─────────────────────────────

        /// <summary>
        /// 在指定格子位置放置炮塔
        /// </summary>
        /// <param name="entityManager">EntityManager</param>
        /// <param name="gridCoord">格子坐标</param>
        /// <param name="towerEntity">炮塔实体</param>
        /// <returns>是否放置成功</returns>
        public static bool TryPlaceTower(EntityManager entityManager, int2 gridCoord, Entity towerEntity)
        {
            if (!TryGetMapDataAndBuffer(entityManager, out var mapData, out var singletonEntity))
                return false;

            // 边界检查
            if (!GridMath.IsInBounds(gridCoord, mapData.Width, mapData.Height))
            {
                _logger.LogWarning("[GridModificationSystem] PlaceTower out of bounds: ({0},{1})",
                    gridCoord.x, gridCoord.y);
                return false;
            }

            int index = GridMath.GridToIndex(gridCoord, mapData.Width, mapData.Height);
            if (index < 0) return false;

            // 检查格子类型是否可放置
            ref var blobData = ref mapData.BlobData.Value;
            var cellType = (CellType)blobData.Cells[index];
            if (!cellType.IsPlaceable())
            {
                _logger.LogWarning("[GridModificationSystem] Cell ({0},{1}) is not placeable, type={2}",
                    gridCoord.x, gridCoord.y, cellType);
                return false;
            }

            // 检查是否已被占据
            var buffer = entityManager.GetBuffer<GridCellState>(singletonEntity);
            if (buffer[index].IsOccupied)
            {
                _logger.LogWarning("[GridModificationSystem] Cell ({0},{1}) is already occupied",
                    gridCoord.x, gridCoord.y);
                return false;
            }

            // 执行放置
            buffer[index] = new GridCellState
            {
                Occupant = towerEntity,
                RuntimeFlags = 0
            };

            // 标记脏
            if (!entityManager.HasComponent<GridDirtyTag>(singletonEntity))
            {
                entityManager.AddComponent<GridDirtyTag>(singletonEntity);
            }

            _logger.LogInformation("[GridModificationSystem] Tower placed at ({0},{1})",
                gridCoord.x, gridCoord.y);
            return true;
        }

        /// <summary>
        /// 从指定格子移除炮塔
        /// </summary>
        public static bool TryRemoveTower(EntityManager entityManager, int2 gridCoord)
        {
            if (!TryGetMapDataAndBuffer(entityManager, out var mapData, out var singletonEntity))
                return false;

            if (!GridMath.IsInBounds(gridCoord, mapData.Width, mapData.Height))
                return false;

            int index = GridMath.GridToIndex(gridCoord, mapData.Width, mapData.Height);
            if (index < 0) return false;

            var buffer = entityManager.GetBuffer<GridCellState>(singletonEntity);
            if (!buffer[index].IsOccupied)
            {
                _logger.LogWarning("[GridModificationSystem] Cell ({0},{1}) has no occupant to remove",
                    gridCoord.x, gridCoord.y);
                return false;
            }

            buffer[index] = GridCellState.Empty;

            if (!entityManager.HasComponent<GridDirtyTag>(singletonEntity))
            {
                entityManager.AddComponent<GridDirtyTag>(singletonEntity);
            }

            _logger.LogInformation("[GridModificationSystem] Tower removed from ({0},{1})",
                gridCoord.x, gridCoord.y);
            return true;
        }

        /// <summary>
        /// 查询指定格子的状态
        /// </summary>
        public static bool TryGetCellState(EntityManager entityManager, int2 gridCoord,
            out CellType cellType, out GridCellState cellState)
        {
            cellType = CellType.Solid;
            cellState = GridCellState.Empty;

            if (!TryGetMapDataAndBuffer(entityManager, out var mapData, out var singletonEntity))
                return false;

            if (!GridMath.IsInBounds(gridCoord, mapData.Width, mapData.Height))
                return false;

            int index = GridMath.GridToIndex(gridCoord, mapData.Width, mapData.Height);
            if (index < 0) return false;

            ref var blobData = ref mapData.BlobData.Value;
            cellType = (CellType)blobData.Cells[index];

            var buffer = entityManager.GetBuffer<GridCellState>(singletonEntity, true);
            cellState = buffer[index];
            return true;
        }

        /// <summary>
        /// 查询指定世界坐标对应的格子信息
        /// </summary>
        public static bool TryGetCellAtWorldPos(EntityManager entityManager, float2 worldPos,
            out int2 gridCoord, out CellType cellType, out GridCellState cellState)
        {
            gridCoord = default;
            cellType = CellType.Solid;
            cellState = GridCellState.Empty;

            if (!TryGetMapDataAndBuffer(entityManager, out var mapData, out _))
                return false;

            gridCoord = GridMath.WorldToGrid(worldPos, mapData.Origin, mapData.CellSize);
            return TryGetCellState(entityManager, gridCoord, out cellType, out cellState);
        }

        /// <summary>
        /// 检查格子是否可以放置炮塔
        /// </summary>
        public static bool CanPlaceAt(EntityManager entityManager, int2 gridCoord)
        {
            if (!TryGetCellState(entityManager, gridCoord, out var cellType, out var cellState))
                return false;

            return cellType.IsPlaceable() && !cellState.IsOccupied;
        }

        /// <summary>
        /// 检查格子是否可通行
        /// </summary>
        public static bool IsWalkableAt(EntityManager entityManager, int2 gridCoord)
        {
            if (!TryGetCellState(entityManager, gridCoord, out var cellType, out var cellState))
                return false;

            return cellType.IsWalkable() && !cellState.IsOccupied;
        }

        // ── 内部工具 ─────────────────────────────────────────

        private static bool TryGetMapDataAndBuffer(EntityManager entityManager,
            out GridMapData mapData, out Entity singletonEntity)
        {
            mapData = default;
            singletonEntity = Entity.Null;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GridMapData>());

            if (query.IsEmpty)
            {
                _logger.LogWarning("[GridModificationSystem] GridMapData singleton not found");
                return false;
            }

            singletonEntity = query.GetSingletonEntity();
            mapData = entityManager.GetComponentData<GridMapData>(singletonEntity);
            return true;
        }
    }
}

