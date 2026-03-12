using Unity.Entities;

namespace TowerDefense.Components.Combat
{
    /// <summary>
    /// 可视化关联标记 — IComponentData
    ///
    /// 标记一个 ECS 实体已被 EcsVisualBridge 关联到一个 GameObject。
    /// VisualId 是 EcsVisualBridge 内部的唯一 ID，用于反查 GameObject。
    /// 添加此标记后，EcsVisualBridge 不会重复创建 GameObject。
    /// </summary>
    public struct VisualLinked : IComponentData
    {
        /// <summary>EcsVisualBridge 分配的唯一 ID</summary>
        public int VisualId;
    }

    /// <summary>
    /// 可视化类型枚举 — 决定实例化哪种预制体
    /// </summary>
    public enum VisualType : byte
    {
        None = 0,
        Tower = 1,
        Enemy = 2,
        Bullet = 3,
    }

    /// <summary>
    /// 可视化请求 — IComponentData
    ///
    /// 创建 ECS 实体时添加此组件，
    /// EcsVisualBridge 检测到后实例化对应 prefab 并替换为 VisualLinked。
    /// </summary>
    public struct VisualRequest : IComponentData
    {
        /// <summary>需要实例化的预制体类型</summary>
        public VisualType Type;
    }
}

