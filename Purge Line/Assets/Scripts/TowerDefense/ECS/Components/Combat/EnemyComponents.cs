using Unity.Entities;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 敌人标记 — Tag Component
    /// 用于快速查询所有敌人实体
    /// </summary>
    public struct EnemyTag : IComponentData { }

    /// <summary>
    /// 敌人静态配置数据 — IComponentData
    ///
    /// 存储敌人的类型信息和奖励。
    /// 移动速度通过 FlowFieldAgent.Speed 控制，此处存储基础速度用于 Buff 计算。
    /// </summary>
    public struct EnemyData : IComponentData
    {
        /// <summary>
        /// 敌人类型 ID — 扩展用
        /// 0 = 普通, 1 = 快速, 2 = 肉盾 ...
        /// </summary>
        public int EnemyTypeId;

        /// <summary>基础移动速度（用于 Buff 系统参照基准）</summary>
        public float BaseSpeed;

        /// <summary>击杀奖励金币</summary>
        public int RewardGold;
    }

    /// <summary>
    /// 生命值组件 — IComponentData
    ///
    /// 通用生命值组件，可挂载在任何需要 HP 的实体上（敌人、建筑等）。
    /// HealthDeathSystem 检查此组件判断死亡。
    /// </summary>
    public struct HealthData : IComponentData
    {
        /// <summary>当前生命值</summary>
        public int CurrentHP;

        /// <summary>最大生命值</summary>
        public int MaxHP;

        /// <summary>是否存活</summary>
        public bool IsAlive => CurrentHP > 0;
    }
}

