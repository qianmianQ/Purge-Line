using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Components
{
    /// <summary>
    /// 流场数据 — 挂载在 GridMapData 同一 Singleton Entity 上
    ///
    /// 存储预计算的流场方向 BlobAsset，供所有 FlowFieldAgent 查询。
    /// 数据在 FlowFieldBakeSystem 中生成，关卡卸载时释放。
    /// </summary>
    public struct FlowFieldData : IComponentData
    {
        /// <summary>流场方向数据（BlobAsset 只读）</summary>
        public BlobAssetReference<FlowFieldBlobData> BlobData;

        /// <summary>烘焙时的数据哈希，用于校验一致性</summary>
        public uint DataHash;

        /// <summary>算法版本号</summary>
        public int AlgorithmVersion;

        /// <summary>目标点数量</summary>
        public int GoalCount;
    }

    /// <summary>
    /// 流场 BlobAsset 数据结构
    /// </summary>
    public struct FlowFieldBlobData
    {
        /// <summary>
        /// 每格一个 byte 的方向编码
        /// 0-7 = 八方向（N, NE, E, SE, S, SW, W, NW），255 = 无方向
        /// </summary>
        public BlobArray<byte> Directions;
    }

    /// <summary>
    /// 流场目标点 — IBufferElementData
    ///
    /// 挂载在 GridMapData singleton entity 上，
    /// 存储关卡中所有目标点的格子坐标。
    /// </summary>
    public struct FlowFieldGoal : IBufferElementData
    {
        /// <summary>目标点格子坐标</summary>
        public int2 GridCoord;
    }

    /// <summary>
    /// 流场烘焙请求 — Tag Component
    ///
    /// 添加到 GridMapData singleton entity 上，
    /// FlowFieldBakeSystem 检测到后执行烘焙并移除此标记。
    /// </summary>
    public struct FlowFieldBakeRequest : IComponentData { }
}
