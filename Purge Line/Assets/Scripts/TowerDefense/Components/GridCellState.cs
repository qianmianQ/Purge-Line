using Unity.Entities;

namespace TowerDefense.Components
{
    /// <summary>
    /// 格子运行时状态 — IBufferElementData
    ///
    /// 挂载在 GridMapData singleton 实体的 DynamicBuffer 上。
    /// 每个元素对应地图中一个格子的运行时可变状态。
    ///
    /// 与 BlobAsset 中的 CellType 不同，此 buffer 用于追踪：
    /// - 格子是否被占据（炮塔放置）
    /// - 占据者的 Entity 引用
    /// - 运行时动态标记
    /// </summary>
    [InternalBufferCapacity(0)] // 不在 chunk 内联存储，使用 heap buffer
    public struct GridCellState : IBufferElementData
    {
        /// <summary>
        /// 占据此格子的实体引用。
        /// Entity.Null 表示格子空闲。
        /// </summary>
        public Entity Occupant;

        /// <summary>
        /// 运行时标记位（预留扩展）
        /// bit 0: 已被选中高亮
        /// bit 1: 被技能影响
        /// bit 2-7: 预留
        /// </summary>
        public byte RuntimeFlags;

        /// <summary>格子是否被占据</summary>
        public bool IsOccupied => Occupant != Entity.Null;

        /// <summary>创建空闲状态的格子</summary>
        public static GridCellState Empty => new GridCellState
        {
            Occupant = Entity.Null,
            RuntimeFlags = 0
        };
    }
}

