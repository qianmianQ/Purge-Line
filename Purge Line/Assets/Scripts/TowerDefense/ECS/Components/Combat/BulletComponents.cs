using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 子弹标记 — Tag Component
    /// 用于快速查询所有子弹实体
    /// </summary>
    public struct BulletTag : IComponentData { }

    /// <summary>
    /// 子弹静态属性 — IComponentData
    ///
    /// 存储子弹的不可变属性（由炮塔配置决定）。
    /// </summary>
    public struct BulletData : IComponentData
    {
        /// <summary>伤害值</summary>
        public int Damage;

        /// <summary>飞行速度（世界单位/秒）</summary>
        public float Speed;

        /// <summary>最大飞行距离（超出后自动销毁）</summary>
        public float MaxRange;

        /// <summary>
        /// 子弹类型 ID — 扩展用
        /// 0 = 普通子弹, 1 = AOE 子弹, 2 = 穿透子弹 ...
        /// </summary>
        public int BulletTypeId;
    }

    /// <summary>
    /// 子弹运行时状态 — IComponentData
    ///
    /// 存储子弹的帧间可变状态。
    /// </summary>
    public struct BulletState : IComponentData
    {
        /// <summary>追踪的目标实体，Entity.Null = 无目标（直线飞行）</summary>
        public Entity TargetEntity;

        /// <summary>子弹起始位置（用于计算飞行距离）</summary>
        public float3 StartPosition;

        /// <summary>已飞行距离</summary>
        public float DistanceTraveled;

        /// <summary>是否已命中目标（防止重复伤害）</summary>
        public byte HasHit; // 0=false, 1=true (使用byte避免bool的Burst兼容问题)

        /// <summary>最后已知的目标位置（目标死亡后继续飞向此处）</summary>
        public float3 LastKnownTargetPos;
    }
}

