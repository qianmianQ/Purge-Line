using Unity.Entities;

namespace TowerDefense.Components
{
    /// <summary>
    /// 地图生成请求 — IComponentData (一次性消费)
    ///
    /// 创建此组件的实体后，GridSpawnSystem 在下一帧消费它：
    /// 1. 根据请求数据构建 BlobAsset
    /// 2. 创建/更新 GridMapData singleton
    /// 3. 初始化 GridCellState buffer
    /// 4. 销毁请求实体
    ///
    /// 使用 IEnableableComponent 而非直接销毁，以支持
    /// 在同一帧内检测到请求并处理。
    /// </summary>
    public struct GridSpawnRequest : IComponentData
    {
        /// <summary>地图宽度（格子数）</summary>
        public int Width;

        /// <summary>地图高度（格子数）</summary>
        public int Height;

        /// <summary>单个格子世界尺寸</summary>
        public float CellSize;

        /// <summary>地图原点 X（世界坐标）</summary>
        public float OriginX;

        /// <summary>地图原点 Y（世界坐标）</summary>
        public float OriginY;

        /// <summary>
        /// 格子数据的 NativeArray 句柄：
        /// 由请求创建者分配，GridSpawnSystem 消费后释放。
        /// 通过 SharedStatic 或 managed companion 传递 byte[] 引用。
        /// </summary>
        public int CellDataId;
    }

    /// <summary>
    /// 网格脏标记 — Tag Component
    ///
    /// 添加到 GridMapData singleton 实体上，表示渲染数据需要刷新。
    /// GridRenderSystem 消费后移除。
    /// </summary>
    public struct GridDirtyTag : IComponentData { }
}

