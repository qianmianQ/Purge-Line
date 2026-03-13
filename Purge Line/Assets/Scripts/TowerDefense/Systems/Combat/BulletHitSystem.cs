using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using TowerDefense.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace TowerDefense.Systems.Combat
{
    /// <summary>
    /// 子弹命中系统 — 检测子弹是否命中目标，造成伤害并标记销毁
    ///
    /// 🔴 关键逻辑约束：
    /// - 子弹一次只能对单个敌人造成一次伤害
    /// - 命中后立即标记销毁（HasHit=1 + DestroyTag）
    /// - 超出最大飞行距离的子弹也在此处理销毁
    ///
    /// 前置系统：BulletMovementSystem（子弹位置已更新）
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletMovementSystem))]
    public partial struct BulletHitSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("BulletHitSystem");
            state.RequireForUpdate<BulletTag>();
            _logger.LogInformation("[BulletHitSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var healthLookup = SystemAPI.GetComponentLookup<HealthData>(false);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var deadTagLookup = SystemAPI.GetComponentLookup<DeadTag>(true);
            var destroyTagLookup = SystemAPI.GetComponentLookup<DestroyTag>(true);

            float hitRadiusSq = CombatConfig.BulletHitRadius * CombatConfig.BulletHitRadius;

            foreach (var (bulletState, bulletData, bulletTransform, entity) in
                SystemAPI.Query<RefRW<BulletState>, RefRO<BulletData>, RefRO<LocalTransform>>()
                    .WithAll<BulletTag>()
                    .WithNone<DestroyTag>()
                    .WithEntityAccess())
            {
                ref var stateRW = ref bulletState.ValueRW;

                // 已标记为超出范围（HasHit=1但还没有DestroyTag）
                if (stateRW.HasHit != 0)
                {
                    ecb.AddComponent<DestroyTag>(entity);
                    continue;
                }

                Entity target = stateRW.TargetEntity;

                // 目标无效 → 检查是否到达最后已知位置
                if (target == Entity.Null ||
                    !transformLookup.HasComponent(target) ||
                    deadTagLookup.HasComponent(target) ||
                    destroyTagLookup.HasComponent(target))
                {
                    // 目标已不存在，检查是否到达了最后已知位置
                    float3 bulletPos = bulletTransform.ValueRO.Position;
                    float3 lastKnown = stateRW.LastKnownTargetPos;
                    float distSq = math.distancesq(bulletPos, lastKnown);

                    if (distSq < hitRadiusSq * 4f || stateRW.DistanceTraveled > bulletData.ValueRO.MaxRange)
                    {
                        // 到达了最后已知位置或超出范围，直接销毁
                        stateRW.HasHit = 1;
                        ecb.AddComponent<DestroyTag>(entity);
                    }
                    continue;
                }

                // 目标有效 → 检测距离
                float3 bPos = bulletTransform.ValueRO.Position;
                float3 tPos = transformLookup[target].Position;
                float dSq = math.distancesq(bPos, tPos);

                if (dSq <= hitRadiusSq)
                {
                    // 🔴 命中！造成伤害
                    if (healthLookup.HasComponent(target))
                    {
                        var health = healthLookup[target];
                        health.CurrentHP -= bulletData.ValueRO.Damage;
                        healthLookup[target] = health;
                    }

                    // 标记子弹已命中 + 待销毁
                    stateRW.HasHit = 1;
                    ecb.AddComponent<DestroyTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

