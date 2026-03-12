using Unity.Entities;
using Unity.Mathematics;

namespace PurgeLine.Events
{
    /// <summary>炮塔放置完成事件</summary>
    public struct TowerPlacedEvent
    {
        public int2 GridCoord;
        public Entity TowerEntity;
        public int TowerTypeId;
    }

    /// <summary>炮塔移除事件</summary>
    public struct TowerRemovedEvent
    {
        public int2 GridCoord;
    }

    /// <summary>敌人死亡事件</summary>
    public struct EnemyDeathEvent
    {
        public Entity EnemyEntity;
        public int RewardGold;
        public float2 DeathPosition;
    }

    /// <summary>敌人到达终点事件</summary>
    public struct EnemyReachedGoalEvent
    {
        public Entity EnemyEntity;
        public float2 GoalPosition;
    }

    /// <summary>子弹命中事件</summary>
    public struct BulletHitEvent
    {
        public Entity BulletEntity;
        public Entity TargetEntity;
        public int Damage;
        public float2 HitPosition;
    }

    /// <summary>波次开始事件</summary>
    public struct WaveStartedEvent
    {
        public int WaveIndex;
    }

    /// <summary>波次完成事件</summary>
    public struct WaveCompletedEvent
    {
        public int WaveIndex;
    }

    /// <summary>放置模式变更事件</summary>
    public struct PlacementModeChangedEvent
    {
        public bool IsActive;
    }
}

