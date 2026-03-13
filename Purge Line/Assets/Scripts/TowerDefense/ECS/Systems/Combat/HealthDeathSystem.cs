using Microsoft.Extensions.Logging;
using TowerDefense.ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 生命值死亡系统 — 检查 HP≤0 的实体并标记死亡
    ///
    /// 将 HP≤0 且未标记 DeadTag 的实体添加 DeadTag。
    /// 上层系统（如特效、奖励计算）可在 EntityCleanupSystem 前检查 DeadTag。
    ///
    /// 前置系统：BulletHitSystem（伤害已结算）
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletHitSystem))]
    public partial struct HealthDeathSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("HealthDeathSystem");
            _logger.LogInformation("[HealthDeathSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<HealthData>>()
                    .WithNone<DeadTag, DestroyTag>()
                    .WithEntityAccess())
            {
                if (health.ValueRO.CurrentHP <= 0)
                {
                    // 添加死亡标记
                    ecb.AddComponent<DeadTag>(entity);
                    // 同时标记待销毁（后续可在此之间插入死亡特效/奖励系统）
                    ecb.AddComponent<DestroyTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


