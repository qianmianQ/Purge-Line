using Unity.Entities;

namespace TowerDefense.Components.Combat
{
    /// <summary>
    /// 炮塔标记 — Tag Component
    /// 用于快速查询所有炮塔实体
    /// </summary>
    public struct TowerTag : IComponentData { }

    /// <summary>
    /// 炮塔静态配置数据 — IComponentData
    ///
    /// 存储炮塔的基础属性，升级时直接修改此组件的值。
    /// 所有字段均为值类型，Burst 友好。
    /// </summary>
    public struct TowerData : IComponentData
    {
        /// <summary>攻击范围（世界单位半径）</summary>
        public float AttackRange;

        /// <summary>攻击间隔（秒）</summary>
        public float AttackInterval;

        /// <summary>子弹飞行速度（世界单位/秒）</summary>
        public float BulletSpeed;

        /// <summary>单次伤害值</summary>
        public int Damage;

        /// <summary>当前等级（1-based）</summary>
        public int Level;

        /// <summary>
        /// 炮塔类型 ID — 扩展用
        /// 0 = 普通炮塔, 1 = AOE 炮塔, 2 = 减速炮塔 ...
        /// </summary>
        public int TowerTypeId;
    }

    /// <summary>
    /// 炮塔运行时状态 — IComponentData
    ///
    /// 存储炮塔的帧间可变状态。
    /// </summary>
    public struct TowerState : IComponentData
    {
        /// <summary>攻击冷却计时器（秒），归零时可发射</summary>
        public float AttackTimer;

        /// <summary>当前锁定的目标敌人 Entity，Entity.Null = 无目标</summary>
        public Entity CurrentTarget;
    }

    /// <summary>
    /// 炮塔升级配置 — IBufferElementData（预留）
    ///
    /// 挂载在炮塔实体上，存储每级升级数据。
    /// 当前版本不使用，预留扩展接口。
    /// </summary>
    public struct TowerUpgradeLevel : IBufferElementData
    {
        public float AttackRange;
        public float AttackInterval;
        public int Damage;
        public float BulletSpeed;
        public int Cost;
    }
}

