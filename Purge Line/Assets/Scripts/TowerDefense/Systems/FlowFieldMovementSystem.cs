using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Systems
{
    /// <summary>
    /// 流场移动系统 — 驱动所有 FlowFieldAgent 沿流场方向移动
    ///
    /// 运行在 SimulationSystemGroup 中，使用 IJobEntity + Burst 编译。
    /// 每个单位 O(1) 方向查询，支持 10万+ 单位并行寻路。
    ///
    /// 前置条件：
    /// - GridMapData singleton 存在
    /// - FlowFieldData 组件已就绪（烘焙完成）
    /// - 代理实体同时具有 LocalTransform 和 FlowFieldAgent 组件
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FlowFieldMovementSystem : ISystem
    {
        private static ILogger _logger;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("FlowFieldMovementSystem");
            state.RequireForUpdate<GridMapData>();
            state.RequireForUpdate<FlowFieldData>();
            _logger.LogInformation("[FlowFieldMovementSystem] Created");
        }

        public void OnDestroy(ref SystemState state)
        {
            _logger.LogInformation("[FlowFieldMovementSystem] Destroyed");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var mapData = SystemAPI.GetSingleton<GridMapData>();
            var ffData = SystemAPI.GetSingleton<FlowFieldData>();

            if (!ffData.BlobData.IsCreated) return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            var moveJob = new FlowFieldMoveJob
            {
                MapWidth = mapData.Width,
                MapHeight = mapData.Height,
                CellSize = mapData.CellSize,
                Origin = mapData.Origin,
                FlowData = ffData.BlobData,
                DeltaTime = deltaTime
            };

            state.Dependency = moveJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 流场移动 Job — Burst 编译 + IJobEntity 并行
    ///
    /// 对每个 FlowFieldAgent 实体：
    /// 1. 读取当前世界坐标
    /// 2. 转换为格子坐标
    /// 3. 查询流场方向
    /// 4. 按方向和速度更新位置
    /// </summary>
    [BurstCompile]
    public partial struct FlowFieldMoveJob : IJobEntity
    {
        public int MapWidth;
        public int MapHeight;
        public float CellSize;
        public float2 Origin;
        [ReadOnly]
        public BlobAssetReference<FlowFieldBlobData> FlowData;
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref FlowFieldAgent agent)
        {
            if (agent.ReachedGoal) return;

            // 当前世界坐标
            float2 worldPos = new float2(transform.Position.x, transform.Position.y);

            // 转换为格子坐标
            float2 local = worldPos - Origin;
            int gx = (int)math.floor(local.x / CellSize);
            int gy = (int)math.floor(local.y / CellSize);

            // 边界检查
            if (gx < 0 || gx >= MapWidth || gy < 0 || gy >= MapHeight)
                return;

            int index = gy * MapWidth + gx;
            ref var flowData = ref FlowData.Value;
            byte direction = flowData.Directions[index];

            // 已到达目标
            if (direction == FlowFieldMath.DirectionGoal)
            {
                agent.Velocity = float2.zero;
                agent.ReachedGoal = true;
                return;
            }

            // 无路径（不可达格子）
            if (direction == FlowFieldMath.DirectionNone)
            {
                agent.Velocity = float2.zero;
                return;
            }

            // 获取移动方向
            float2 moveDir = FlowFieldMath.DirectionToVector(direction);
            float2 velocity = moveDir * agent.Speed;
            agent.Velocity = velocity;

            // 更新位置
            float3 delta = new float3(velocity.x, velocity.y, 0f) * DeltaTime;
            transform.Position += delta;
        }
    }
}
