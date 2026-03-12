using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TowerDefense.Systems.Combat
{
    /// <summary>
    /// 实体清理系统 — 处理所有带 DestroyTag 的实体
    ///
    /// 在 SimulationSystemGroup 末尾运行，统一销毁标记了 DestroyTag 的实体。
    /// 后续可扩展为对象池回收（不销毁，而是 Disable + 移到回收区）。
    ///
    /// 性能考量：
    /// - 使用 ECB 批量销毁，避免逐个调用 EntityManager.DestroyEntity
    /// - 预留对象池接口，高频创建场景下可无缝切换
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct EntityCleanupSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("EntityCleanupSystem");
            state.RequireForUpdate<DestroyTag>();
            _logger.LogInformation("[EntityCleanupSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<DestroyTag>>()
                    .WithEntityAccess())
            {
                // ── 预留对象池回收接口 ────────────────────────────
                // 高频创建场景下可替换为：
                //   ecb.SetEnabled(entity, false);  // Disable 实体
                //   ecb.AddComponent<DisabledTag>(entity);
                //   ecb.SetComponent(entity, LocalTransform.FromPosition(poolPos));
                // 然后在 Spawn 系统中优先从池中取回
                // ────────────────────────────────────────────────

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


