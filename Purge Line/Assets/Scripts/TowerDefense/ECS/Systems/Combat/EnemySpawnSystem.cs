using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.ECS;
using TowerDefense.Data;
using TowerDefense.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 敌人生成系统 — 按配置频率在出生点生成敌人实体
    ///
    /// 运行在 SimulationSystemGroup 中。
    /// 从 EnemySpawnTimer singleton 读取生成参数，
    /// 使用 ECB 批量创建敌人实体。
    ///
    /// 前置条件：
    /// - GridMapData singleton 存在（地图已加载）
    /// - FlowFieldData 已就绪（流场已烘焙）
    /// - EnemySpawnTimer singleton 存在（由 CombatBridgeSystem 创建）
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct EnemySpawnSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("EnemySpawnSystem");
            state.RequireForUpdate<GridMapData>();
            state.RequireForUpdate<FlowFieldData>();
            state.RequireForUpdate<EnemySpawnTimer>();
            _logger.LogInformation("[EnemySpawnSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            var ffData = SystemAPI.GetSingleton<FlowFieldData>();
            if (!ffData.BlobData.IsCreated) return;

            // 读取生成计时器
            var timerEntity = SystemAPI.GetSingletonEntity<EnemySpawnTimer>();
            var timer = SystemAPI.GetSingleton<EnemySpawnTimer>();
            var spawnPoints = state.EntityManager.GetBuffer<SpawnPointData>(timerEntity, true);

            if (spawnPoints.Length == 0) return;

            // 检查最大数量限制
            if (timer.MaxSpawnCount > 0 && timer.SpawnedCount >= timer.MaxSpawnCount)
                return;

            // 更新计时器
            timer.Timer += SystemAPI.Time.DeltaTime;

            if (timer.Timer < timer.SpawnInterval)
            {
                SystemAPI.SetSingleton(timer);
                return;
            }

            // 触发生成
            timer.Timer -= timer.SpawnInterval;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var mapData = SystemAPI.GetSingleton<GridMapData>();

            // 使用基于帧数的简单随机（Burst 兼容）
            uint seed = (uint)(SystemAPI.Time.ElapsedTime * 1000 + 1);
            var random = new Random(seed);

            int batchSize = timer.BatchSize;
            if (timer.MaxSpawnCount > 0)
            {
                batchSize = math.min(batchSize, timer.MaxSpawnCount - timer.SpawnedCount);
            }

            for (int i = 0; i < batchSize; i++)
            {
                // 随机选择出生点
                int spIdx = random.NextInt(0, spawnPoints.Length);
                float2 spawnWorldPos = spawnPoints[spIdx].WorldPosition;

                // 添加少量随机偏移，避免完全重叠
                float2 offset = new float2(
                    random.NextFloat(-0.3f, 0.3f),
                    random.NextFloat(-0.3f, 0.3f)
                );
                spawnWorldPos += offset;

                // 创建敌人实体
                var enemyEntity = ecb.CreateEntity();

                // 核心 Transform
                ecb.AddComponent(enemyEntity, LocalTransform.FromPosition(
                    new float3(spawnWorldPos.x, spawnWorldPos.y, 0f)));

                // 敌人标记
                ecb.AddComponent<EnemyTag>(enemyEntity);

                // 敌人数据
                ecb.AddComponent(enemyEntity, new EnemyData
                {
                    EnemyTypeId = 0,
                    BaseSpeed = CombatConfig.DefaultEnemySpeed,
                    RewardGold = CombatConfig.DefaultEnemyReward
                });

                // 生命值
                ecb.AddComponent(enemyEntity, new HealthData
                {
                    CurrentHP = CombatConfig.DefaultEnemyHP,
                    MaxHP = CombatConfig.DefaultEnemyHP
                });

                // 流场代理 — 复用已有流场寻路系统
                ecb.AddComponent(enemyEntity, new FlowFieldAgent
                {
                    Speed = CombatConfig.DefaultEnemySpeed,
                    Velocity = float2.zero,
                    ReachedGoal = false
                });

                // 可视化请求 — EcsVisualBridge 检测后实例化 Enemy prefab
                ecb.AddComponent(enemyEntity, new VisualPrefab
                {
                    PrefabAddress = CombatConfig.EnemyPrefabAddress
                });

                timer.SpawnedCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            SystemAPI.SetSingleton(timer);
        }
    }
}

