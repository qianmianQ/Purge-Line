using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Components
{
    /// <summary>
    /// 流场寻路代理 — 标记需要沿流场移动的实体
    ///
    /// 挂载在任何需要寻路的单位（敌人、NPC 等）上。
    /// FlowFieldMovementSystem 读取此组件驱动移动。
    ///
    /// 要求实体同时具有 Unity.Transforms.LocalTransform 组件。
    /// </summary>
    public struct FlowFieldAgent : IComponentData
    {
        /// <summary>移动速度（世界单位/秒）</summary>
        public float Speed;

        /// <summary>当前平滑速度向量（用于视觉插值和动画）</summary>
        public float2 Velocity;

        /// <summary>到达目标后是否自动标记为已到达</summary>
        public bool ReachedGoal;
    }

    /// <summary>
    /// 已到达目标标记 — Tag Component
    ///
    /// 注意：FlowFieldMovementSystem 中的 IJobEntity 无法直接添加组件。
    /// 上层系统应检查 FlowFieldAgent.ReachedGoal == true 并通过 ECB 添加此标记。
    /// 示例用法：在 SimulationSystemGroup 尾部运行清理系统，查询 ReachedGoal 并添加此 tag。
    /// </summary>
    public struct FlowFieldAgentReachedGoal : IComponentData { }
}
