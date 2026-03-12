using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using TowerDefense.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Systems.Combat
{
    /// <summary>
    /// 炮塔搜敌系统 — 为每座炮塔寻找攻击范围内最近的敌人
    ///
    /// 算法：
    /// 1. 先构建敌人空间哈希表 (NativeParallelMultiHashMap)
    /// 2. 每座炮塔仅查询攻击范围覆盖的哈希桶
    /// 3. 选择距离最近的敌人作为目标
    ///
    /// 性能：O(E + T×K)，E=敌人数 T=炮塔数 K=范围内格子数
    /// 支持 10万敌人 + 数千炮塔
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldMovementSystem))]
    public partial struct TowerTargetingSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("TowerTargetingSystem");
            state.RequireForUpdate<TowerTag>();
            _logger.LogInformation("[TowerTargetingSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            // ── Step 1: 构建敌人空间哈希 ──────────────────────────

            // 计算敌人数量
            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform>()
                .WithNone<DeadTag, DestroyTag>()
                .Build();
            int enemyCount = enemyQuery.CalculateEntityCount();

            if (enemyCount == 0)
            {
                // 无敌人，清除所有炮塔目标
                foreach (var (towerState, _) in
                    SystemAPI.Query<RefRW<TowerState>, RefRO<TowerData>>())
                {
                    towerState.ValueRW.CurrentTarget = Entity.Null;
                }
                return;
            }

            // 构建空间哈希
            var spatialMap = new NativeParallelMultiHashMap<int, SpatialEnemyInfo>(
                enemyCount, Allocator.TempJob);

            // 填充空间哈希 Job
            var buildHashJob = new BuildEnemySpatialHashJob
            {
                SpatialMap = spatialMap.AsParallelWriter()
            };
            state.Dependency = buildHashJob.ScheduleParallel(state.Dependency);

            // ── Step 2: 炮塔搜敌 Job ─────────────────────────────

            var targetingJob = new TowerFindTargetJob
            {
                SpatialMap = spatialMap
            };
            state.Dependency = targetingJob.ScheduleParallel(state.Dependency);

            // 确保本帧完成后释放
            state.Dependency.Complete();
            spatialMap.Dispose();
        }
    }

    /// <summary>
    /// 构建敌人空间哈希 Job — Burst + 并行
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    [WithNone(typeof(DeadTag), typeof(DestroyTag))]
    public partial struct BuildEnemySpatialHashJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int, SpatialEnemyInfo>.ParallelWriter SpatialMap;

        private void Execute(Entity entity, in LocalTransform transform)
        {
            float2 pos = new float2(transform.Position.x, transform.Position.y);
            int hash = SpatialHash.Hash(pos);
            SpatialMap.Add(hash, new SpatialEnemyInfo
            {
                Entity = entity,
                Position = pos
            });
        }
    }

    /// <summary>
    /// 炮塔搜敌 Job — Burst + 并行
    ///
    /// 对每座炮塔，查询攻击范围覆盖的空间哈希桶，
    /// 找到距离最近的敌人设为目标。
    /// </summary>
    [BurstCompile]
    public partial struct TowerFindTargetJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, SpatialEnemyInfo> SpatialMap;

        private void Execute(ref TowerState towerState, in TowerData towerData,
            in LocalTransform transform)
        {
            float2 towerPos = new float2(transform.Position.x, transform.Position.y);
            float range = towerData.AttackRange;
            float rangeSq = range * range;

            // 获取需要查询的格子范围
            SpatialHash.GetQueryRange(towerPos, range,
                out int minCX, out int minCY, out int maxCX, out int maxCY);

            Entity bestTarget = Entity.Null;
            float bestDistSq = float.MaxValue;

            // 遍历范围内的所有空间哈希桶
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    int hash = SpatialHash.HashCell(cx, cy);

                    if (!SpatialMap.TryGetFirstValue(hash, out var info, out var it))
                        continue;

                    do
                    {
                        float2 diff = info.Position - towerPos;
                        float distSq = math.lengthsq(diff);

                        // 在攻击范围内且更近
                        if (distSq <= rangeSq && distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestTarget = info.Entity;
                        }
                    } while (SpatialMap.TryGetNextValue(out info, ref it));
                }
            }

            towerState.CurrentTarget = bestTarget;
        }
    }
}




