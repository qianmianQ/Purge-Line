using Unity.Entities;
using UnityEngine;

namespace TowerDefense.ECS
{
    // 标记需要生成视图的实体
    // 包含 Prefab 的Address地址
    public struct VisualPrefab : IComponentData
    {
        public Unity.Collections.FixedString64Bytes PrefabAddress; 
    }

    // 2. 运行时链接组件
    // 存储实例化出来的 GameObject 引用，用于同步位置和销毁
    // 使用 ICleanupComponentData 确保实体销毁时有机会回收 GameObject
    public struct VisualLink : ICleanupComponentData
    {
        public UnityObjectRef<GameObject> GameObjectRef;
        public Unity.Collections.FixedString64Bytes PrefabAddress;
    }
    
    /// <summary>
    /// 可视化关联标记 — IComponentData
    ///
    /// 标记一个 ECS 实体已被 EcsVisualBridge 关联到一个 GameObject。
    /// VisualId 是 EcsVisualBridge 内部的唯一 ID，用于反查 GameObject。
    /// 添加此标记后，EcsVisualBridge 不会重复创建 GameObject。
    /// </summary>
    // public struct VisualLinked : IComponentData
    // {
    //     /// <summary>EcsVisualBridge 分配的唯一 ID</summary>
    //     public int VisualId;
    // }

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
    // public struct VisualRequest : IComponentData
    // {
    //     /// <summary>需要实例化的预制体类型</summary>
    //     public VisualType Type;
    // }
    
    /// <summary>
    /// 视图清理请求 — IComponentData
    ///
    /// 由 Burst 编译的 ISystem（如 EntityCleanupSystem）添加此组件，
    /// 标记该实体需要回收其关联的 GameObject 视图。
    /// 托管的 VisualCleanupSystem 检测到后执行实际回收，然后销毁实体。
    ///
    /// 性能优化：
    /// - Burst 编译的系统只添加轻量级标记组件，不访问托管对象
    /// - 实际的 GameObject 回收延迟到托管 System 批量处理
    /// - 利用 ECS 的 chunk 内存布局实现高速遍历
    /// </summary>
    // public struct VisualCleanupRequest : IComponentData
    // {
    //     /// <summary>回收原因，用于调试和日志</summary>
    //     public CleanupReason Reason;
    // }

    /// <summary>
    /// 清理原因枚举
    /// </summary>
    // public enum CleanupReason : byte
    // {
    //     /// <summary>实体自然生命周期结束（如敌人到达目标）</summary>
    //     LifecycleEnd = 0,
    //
    //     /// <summary>实体被销毁（如被击杀）</summary>
    //     Destroyed = 1,
    //
    //     /// <summary>场景切换或系统关闭</summary>
    //     SystemShutdown = 2,
    //
    //     /// <summary>其他原因</summary>
    //     Other = 255,
    // }
}

