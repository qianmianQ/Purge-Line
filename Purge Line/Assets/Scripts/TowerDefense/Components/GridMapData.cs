using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Components
{
    /// <summary>
    /// 地图网格元数据 — Singleton IComponentData
    ///
    /// 存储地图的基本参数和格子数据的 BlobAsset 引用。
    /// 整个地图只有一个此组件实例，挂载在 Singleton Entity 上。
    ///
    /// BlobAsset 设计理由：
    /// - 格子数据在地图加载后基本不变（放置炮塔通过 GridCellState buffer 追踪，不修改底层类型）
    /// - BlobAsset 读取无 chunk 碎片，天然 Burst 友好
    /// - 支持 Job 中安全只读访问
    /// </summary>
    public struct GridMapData : IComponentData
    {
        /// <summary>地图宽度（格子数）</summary>
        public int Width;

        /// <summary>地图高度（格子数）</summary>
        public int Height;

        /// <summary>单个格子的世界尺寸（正方形边长）</summary>
        public float CellSize;

        /// <summary>地图左下角在世界空间中的坐标（原点）</summary>
        public float2 Origin;

        /// <summary>格子类型数据（BlobAsset 只读数组）</summary>
        public BlobAssetReference<GridBlobData> BlobData;

        /// <summary>格子总数</summary>
        public int CellCount => Width * Height;
    }

    /// <summary>
    /// BlobAsset 中存储的格子数据
    /// </summary>
    public struct GridBlobData
    {
        /// <summary>扁平化格子类型数组，索引 = y * Width + x</summary>
        public BlobArray<byte> Cells;
    }
}

