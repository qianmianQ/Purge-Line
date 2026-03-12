using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Components.Combat
{
    /// <summary>
    /// 死亡标记 — Tag Component
    ///
    /// 由 HealthDeathSystem 添加到 HP≤0 的实体上。
    /// 上层系统（如特效、奖励）可在 EntityCleanupSystem 处理前检查此标记。
    /// </summary>
    public struct DeadTag : IComponentData { }

    /// <summary>
    /// 销毁请求标记 — Tag Component
    ///
    /// 添加此标记的实体将在 EntityCleanupSystem 中被销毁或回收到对象池。
    /// 子弹命中、敌人死亡、敌人到达终点等场景均通过此标记统一处理。
    /// </summary>
    public struct DestroyTag : IComponentData { }

    /// <summary>
    /// 敌人生成计时器 — Singleton IComponentData
    ///
    /// 控制敌人的生成频率和总数。
    /// 挂载在专用 singleton entity 上。
    /// </summary>
    public struct EnemySpawnTimer : IComponentData
    {
        /// <summary>生成间隔（秒）</summary>
        public float SpawnInterval;

        /// <summary>当前计时器</summary>
        public float Timer;

        /// <summary>已生成总数</summary>
        public int SpawnedCount;

        /// <summary>最大生成数量（0=无限）</summary>
        public int MaxSpawnCount;

        /// <summary>单次生成数量</summary>
        public int BatchSize;
    }

    /// <summary>
    /// 出生点数据 — IBufferElementData
    ///
    /// 挂载在 EnemySpawnTimer singleton entity 上，
    /// 存储所有敌人出生点的世界坐标。
    /// </summary>
    public struct SpawnPointData : IBufferElementData
    {
        /// <summary>出生点世界坐标</summary>
        public float2 WorldPosition;
    }

    /// <summary>
    /// 战斗配置数据 — Singleton IComponentData
    ///
    /// 存储全局战斗参数，由 CombatBridgeSystem 创建和更新。
    /// </summary>
    public struct CombatConfigData : IComponentData
    {
        /// <summary>默认炮塔攻击范围</summary>
        public float DefaultTowerRange;

        /// <summary>默认炮塔攻击间隔</summary>
        public float DefaultTowerInterval;

        /// <summary>默认炮塔伤害</summary>
        public int DefaultTowerDamage;

        /// <summary>默认子弹速度</summary>
        public float DefaultBulletSpeed;

        /// <summary>默认敌人HP</summary>
        public int DefaultEnemyHP;

        /// <summary>默认敌人速度</summary>
        public float DefaultEnemySpeed;

        /// <summary>子弹命中半径</summary>
        public float BulletHitRadius;

        /// <summary>敌人生成间隔</summary>
        public float EnemySpawnInterval;
    }
}

