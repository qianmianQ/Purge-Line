using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using TowerDefense.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Systems.Combat
{
    /// <summary>
    /// 子弹飞行系统 — 驱动所有子弹朝目标飞行
    ///
    /// 设计要点：
    /// IJobEntity 遍历 BulletTag 实体时会自动获取 ref LocalTransform（可写），
    /// 如果同时传入 ComponentLookup&lt;LocalTransform&gt;（只读），ECS Safety 会报
    /// aliasing 错误——即使一个写、一个读，因为它们的 TypeIndex 相同。
    ///
    /// 解决方案：
    /// 在主线程先把子弹需要的「目标 Entity → 世界坐标」映射收集到
    /// NativeHashMap&lt;Entity, float3&gt;，Job 里只读这个 HashMap，
    /// 完全不碰 ComponentLookup&lt;LocalTransform&gt;。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TowerAttackSystem))]
    public partial struct BulletMovementSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("BulletMovementSystem");
            state.RequireForUpdate<BulletTag>();
            _logger.LogInformation("[BulletMovementSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            // ── Step 1: 收集所有子弹关注的目标 Entity 集合 ─────────
            //    遍历所有子弹，把 BulletState.TargetEntity 加入集合

            var targetEntities = new NativeHashSet<Entity>(128, Allocator.TempJob);

            foreach (var bulletState in
                SystemAPI.Query<RefRO<BulletState>>()
                    .WithAll<BulletTag>()
                    .WithNone<DestroyTag>())
            {
                var target = bulletState.ValueRO.TargetEntity;
                if (target != Entity.Null)
                    targetEntities.Add(target);
            }

            // ── Step 2: 构建 Entity → Position 映射 ──────────────
            //    只查一次 ComponentLookup，在主线程安全地读取

            var targetPositions = new NativeHashMap<Entity, float3>(
                targetEntities.Count, Allocator.TempJob);

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var deadTagLookup   = SystemAPI.GetComponentLookup<DeadTag>(true);
            var destroyTagLookup = SystemAPI.GetComponentLookup<DestroyTag>(true);

            foreach (var entity in targetEntities)
            {
                bool alive = transformLookup.HasComponent(entity)
                          && !deadTagLookup.HasComponent(entity)
                          && !destroyTagLookup.HasComponent(entity);

                if (alive)
                    targetPositions[entity] = transformLookup[entity].Position;
                // 不存活的目标不放进去，Job 里找不到就用 LastKnownTargetPos
            }

            targetEntities.Dispose();

            // ── Step 3: 调度 Burst Job ────────────────────────────

            float dt = SystemAPI.Time.DeltaTime;

            var moveJob = new BulletMoveJob
            {
                DeltaTime = dt,
                TargetPositions = targetPositions
            };

            state.Dependency = moveJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();          // 确保帧内完成
            targetPositions.Dispose();
        }
    }

    /// <summary>
    /// 子弹移动 Job — Burst 编译 + IJobEntity 并行
    ///
    /// 不再持有 ComponentLookup&lt;LocalTransform&gt;，
    /// 改为读取预构建的 NativeHashMap&lt;Entity, float3&gt;，
    /// 彻底消除 aliasing。
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(BulletTag))]
    [WithNone(typeof(DestroyTag))]
    public partial struct BulletMoveJob : IJobEntity
    {
        public float DeltaTime;

        /// <summary>目标 Entity → 世界坐标 映射（只读）</summary>
        [ReadOnly] public NativeHashMap<Entity, float3> TargetPositions;

        private void Execute(ref LocalTransform transform, in BulletData bulletData,
            ref BulletState bulletState)
        {
            // 已命中，不再移动
            if (bulletState.HasHit != 0) return;

            float3 currentPos = transform.Position;
            Entity target = bulletState.TargetEntity;

            // 确定目标位置
            float3 targetPos;

            if (target != Entity.Null && TargetPositions.TryGetValue(target, out float3 livePos))
            {
                // 目标仍存活，追踪实时位置
                targetPos = livePos;
                bulletState.LastKnownTargetPos = targetPos;
            }
            else
            {
                // 目标死亡/被销毁 → 飞向最后已知位置
                targetPos = bulletState.LastKnownTargetPos;
            }

            // 计算飞行方向
            float3 direction = targetPos - currentPos;
            float dist = math.length(direction);

            if (dist < 0.001f)
            {
                bulletState.HasHit = 1;
                return;
            }

            direction = math.normalize(direction);

            // 移动
            float moveDistance = bulletData.Speed * DeltaTime;
            float3 newPos = currentPos + direction * moveDistance;
            transform.Position = newPos;

            // 累加飞行距离
            bulletState.DistanceTraveled += moveDistance;

            // 超出最大范围 → 标记需要销毁
            if (bulletState.DistanceTraveled > bulletData.MaxRange)
            {
                bulletState.HasHit = 1;
            }
        }
    }
}

