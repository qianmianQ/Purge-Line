using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 敌人到达终点系统 — 检测 FlowFieldAgent.ReachedGoal 并处理
    ///
    /// 🔴 关键逻辑：
    /// - 敌人到达目标点后触发扣血逻辑（预留接口）
    /// - 添加 DestroyTag 标记销毁
    ///
    /// 前置系统：FlowFieldMovementSystem（移动已完成）
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HealthDeathSystem))]
    public partial struct EnemyGoalReachedSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("EnemyGoalReachedSystem");
            _logger.LogInformation("[EnemyGoalReachedSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (agent, transform, entity) in
                SystemAPI.Query<RefRO<FlowFieldAgent>, RefRO<LocalTransform>>()
                    .WithAll<EnemyTag>()
                    .WithNone<DestroyTag>()
                    .WithEntityAccess())
            {
                if (!agent.ValueRO.ReachedGoal) continue;

                // ── 预留接口：敌人到达终点扣血逻辑 ────────────────
                // TODO: 扣减玩家基地生命值
                // 可通过事件系统或直接修改全局状态实现
                // EventManager.Gameplay.Dispatch(new EnemyReachedGoalEvent { ... });
                // ─────────────────────────────────────────────────

                // 标记销毁
                ecb.AddComponent<DestroyTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


