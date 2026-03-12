using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Systems.Combat
{
    /// <summary>
    /// 炮塔攻击系统 — 按攻击间隔发射子弹
    ///
    /// 前置系统：TowerTargetingSystem（确保目标已更新）
    ///
    /// 流程：
    /// 1. 更新攻击计时器
    /// 2. 计时器归零 + 有有效目标 → 通过 ECB 创建子弹实体
    /// 3. 子弹带有 BulletData + BulletState + LocalTransform
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TowerTargetingSystem))]
    public partial struct TowerAttackSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("TowerAttackSystem");
            state.RequireForUpdate<TowerTag>();
            _logger.LogInformation("[TowerAttackSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            float dt = SystemAPI.Time.DeltaTime;

            // 需要通过 ComponentLookup 验证目标仍存在且未死亡
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var deadTagLookup = SystemAPI.GetComponentLookup<DeadTag>(true);
            var destroyTagLookup = SystemAPI.GetComponentLookup<DestroyTag>(true);

            foreach (var (towerState, towerData, towerTransform) in
                SystemAPI.Query<RefRW<TowerState>, RefRO<TowerData>, RefRO<LocalTransform>>()
                    .WithAll<TowerTag>())
            {
                ref var stateRW = ref towerState.ValueRW;

                // 更新攻击计时器
                stateRW.AttackTimer += dt;

                // 验证目标有效性
                Entity target = stateRW.CurrentTarget;
                if (target == Entity.Null) continue;

                // 目标已死亡或被销毁
                if (!localTransformLookup.HasComponent(target) ||
                    deadTagLookup.HasComponent(target) ||
                    destroyTagLookup.HasComponent(target))
                {
                    stateRW.CurrentTarget = Entity.Null;
                    continue;
                }

                // 检查攻击间隔
                if (stateRW.AttackTimer < towerData.ValueRO.AttackInterval)
                    continue;

                // 发射子弹
                stateRW.AttackTimer = 0f;

                float3 towerPos = towerTransform.ValueRO.Position;
                float3 targetPos = localTransformLookup[target].Position;

                // 创建子弹实体
                var bulletEntity = ecb.CreateEntity();

                ecb.AddComponent(bulletEntity, LocalTransform.FromPosition(towerPos));
                ecb.AddComponent<BulletTag>(bulletEntity);

                ecb.AddComponent(bulletEntity, new BulletData
                {
                    Damage = towerData.ValueRO.Damage,
                    Speed = towerData.ValueRO.BulletSpeed,
                    MaxRange = towerData.ValueRO.AttackRange * 1.5f, // 飞行距离 = 攻击范围 × 1.5
                    BulletTypeId = 0
                });

                ecb.AddComponent(bulletEntity, new BulletState
                {
                    TargetEntity = target,
                    StartPosition = towerPos,
                    DistanceTraveled = 0f,
                    HasHit = 0,
                    LastKnownTargetPos = targetPos
                });

                // 可视化请求 — EcsVisualBridge 检测后实例化 Bullet prefab
                ecb.AddComponent(bulletEntity, new VisualRequest
                {
                    Type = VisualType.Bullet
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


