using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Data.Blueprint
{
    // 批量实体实例化工具
    //
    // 两种使用模式：
    //   1. SpawnBatch()         — 主线程 EntityManager.Instantiate（适合小批量 / 简单场景）
    //   2. ScheduleSpawnBatch() — ECB 并行 Job（适合大规模 Spawn，释放主线程瓶颈）
    //
    // 架构限制：
    //   - SpawnBatch() 使用 em.Instantiate（主线程 API），大规模使用仍有主线程瓶颈
    //   - ScheduleSpawnBatch() 中 ECB.Instantiate 不支持 NativeArray<Entity> out 参数
    //     （EntityManager.Instantiate 才支持），ECB 版本通过延迟 Entity 句柄写入 position
    //   - 蓝图中必须声明 LocalTransform，否则 position 写入将报错（SetComponent 要求组件已存在）
    //   - ECB 的 Playback / Dispose 由调用者负责
    public static class BlueprintSpawner
    {
        // ── 主线程批量实例化 ─────────────────────────────────────────────────────────
        // em.Instantiate 是 chunk-level memcpy，极快，但受限于主线程
        // positions 和 outEntities 必须等长
        public static void SpawnBatch(
            EntityManager em,
            Entity prefab,
            NativeArray<float3> positions,
            NativeArray<Entity> outEntities)
        {
            // chunk memcpy：一次性复制整个 Prefab Entity 的所有组件值到目标 Entity
            em.Instantiate(prefab, outEntities);

            // 仅 patch 位置（各 Entity 位置不同，覆盖蓝图中的默认值）
            for (int i = 0; i < outEntities.Length; i++)
                em.SetComponentData(outEntities[i], LocalTransform.FromPosition(positions[i]));
        }

        // ── ECB 并行实例化（大规模 Spawn 推荐方案）───────────────────────────────────
        // 将 Instantiate + SetComponent 打包进 IJobParallelFor，充分利用多线程
        //
        // 使用要求：
        //   ecb 必须来自支持并发写入的 EntityCommandBuffer（通常从 EntityCommandBufferSystem
        //   获取，或 new EntityCommandBuffer(Allocator.TempJob).AsParallelWriter()）
        //   Job 完成后调用者负责 ecb.Playback(em) 和 ecb.Dispose()
        //
        // 返回 JobHandle 供调用者链式依赖
        public static JobHandle ScheduleSpawnBatch(
            NativeArray<float3> positions,
            Entity prefab,
            EntityCommandBuffer.ParallelWriter ecb,
            JobHandle dependency = default)
        {
            var job = new SpawnBatchJob
            {
                Positions   = positions,
                PrefabEntity = prefab,
                Ecb          = ecb
            };
            return job.Schedule(positions.Length, 64, dependency);
        }

        // ── 仅 patch 位置（已完成 Instantiate 后的批次）────────────────────────────
        // 适用于：已通过 em.Instantiate 填充了 outEntities，需要并行写入不同位置的场景
        // 需确保 entities 数组对应的 Entity 都具有 LocalTransform 组件（蓝图中已声明）
        public static JobHandle SchedulePatchTransforms(
            NativeArray<Entity> entities,
            NativeArray<float3> positions,
            ComponentLookup<LocalTransform> transformLookup,
            JobHandle dependency = default)
        {
            var job = new PatchTransformJob
            {
                Entities   = entities,
                Positions  = positions,
                Transforms = transformLookup
            };
            return job.Schedule(entities.Length, 64, dependency);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 内部 Job 定义
        // ─────────────────────────────────────────────────────────────────────────────

        // ECB 并行实例化 Job
        // 每个线程独立 Instantiate 一个 Entity 并写入对应位置
        // Burst 编译，极低开销
        [BurstCompile]
        private struct SpawnBatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public Entity PrefabEntity;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(int i)
            {
                // Instantiate 返回延迟 Entity（ECB Playback 后才分配真实 Entity）
                Entity e = Ecb.Instantiate(i, PrefabEntity);
                // 覆盖默认 LocalTransform，写入各 Entity 独立的世界坐标
                Ecb.SetComponent(i, e, LocalTransform.FromPosition(Positions[i]));
            }
        }

        // 并行 patch 位置（已 Instantiate 完毕，仅覆盖 LocalTransform）
        [BurstCompile]
        private struct PatchTransformJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public NativeArray<float3> Positions;
            // 写访问，各线程写入不同 Entity，无数据竞争
            public ComponentLookup<LocalTransform> Transforms;

            public void Execute(int i)
                => Transforms[Entities[i]] = LocalTransform.FromPosition(Positions[i]);
        }
    }
}
