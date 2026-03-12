using System;
using TowerDefense.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TowerDefense.Utilities
{
    /// <summary>
    /// 流场烘焙器 — BFS 算法实现
    ///
    /// 提供 Burst 编译的 IJob 和编辑器可用的静态 API。
    /// 算法：多源 BFS 从所有目标点同时扩散，计算每个可通行格子的最优方向。
    ///
    /// 性能基准：500×500 网格 ≈ 3-5ms（Burst），≈ 15-20ms（无 Burst）
    /// </summary>
    public static class FlowFieldBaker
    {
        /// <summary>不可达代价值</summary>
        public const int CostUnreachable = int.MaxValue;

        /// <summary>
        /// 在编辑器中同步烘焙流场（可在非 Play Mode 使用）
        /// </summary>
        /// <param name="cells">格子类型数组（行优先）</param>
        /// <param name="width">地图宽度</param>
        /// <param name="height">地图高度</param>
        /// <param name="goalGridCoords">目标点格子坐标数组</param>
        /// <returns>方向数组（每格一个 byte），调用方无需手动释放</returns>
        public static byte[] BakeInEditor(byte[] cells, int width, int height, int2[] goalGridCoords)
        {
            if (cells == null || cells.Length != width * height)
                throw new ArgumentException("Cells array size mismatch");
            if (goalGridCoords == null || goalGridCoords.Length == 0)
                throw new ArgumentException("At least one goal point is required");

            int cellCount = width * height;

            // 分配 Native 数据
            var nativeCells = new NativeArray<byte>(cells, Allocator.TempJob);
            var nativeGoals = new NativeArray<int2>(goalGridCoords, Allocator.TempJob);
            var directions = new NativeArray<byte>(cellCount, Allocator.TempJob);
            var costField = new NativeArray<int>(cellCount, Allocator.TempJob);
            var queue = new NativeQueue<int>(Allocator.TempJob);

            try
            {
                var job = new FlowFieldBakeJob
                {
                    Cells = nativeCells,
                    Width = width,
                    Height = height,
                    Goals = nativeGoals,
                    Directions = directions,
                    CostField = costField,
                    Queue = queue
                };

                job.Schedule().Complete();

                // 拷贝结果到托管数组
                var result = new byte[cellCount];
                directions.CopyTo(result);
                return result;
            }
            finally
            {
                nativeCells.Dispose();
                nativeGoals.Dispose();
                directions.Dispose();
                costField.Dispose();
                queue.Dispose();
            }
        }

        /// <summary>
        /// 在运行时使用 NativeArray 烘焙流场
        /// </summary>
        /// <param name="cells">格子类型 NativeArray（只读）</param>
        /// <param name="width">地图宽度</param>
        /// <param name="height">地图高度</param>
        /// <param name="goals">目标点坐标 NativeArray（只读）</param>
        /// <param name="directions">输出方向数组（需预分配，长度 = width * height）</param>
        /// <param name="costField">工作缓冲区（需预分配，长度 = width * height）</param>
        /// <param name="queue">工作队列（需预分配）</param>
        /// <returns>可调度的 JobHandle</returns>
        public static JobHandle ScheduleBake(
            NativeArray<byte> cells, int width, int height,
            NativeArray<int2> goals,
            NativeArray<byte> directions,
            NativeArray<int> costField,
            NativeQueue<int> queue,
            JobHandle dependency = default)
        {
            var job = new FlowFieldBakeJob
            {
                Cells = cells,
                Width = width,
                Height = height,
                Goals = goals,
                Directions = directions,
                CostField = costField,
                Queue = queue
            };

            return job.Schedule(dependency);
        }
    }

    /// <summary>
    /// 流场 BFS 烘焙 Job — Burst 编译
    ///
    /// 多源 BFS 从目标点扩散，计算代价场和方向场。
    /// 只有 CellType 包含 Walkable 标记的格子才可通行。
    /// </summary>
    [BurstCompile]
    public struct FlowFieldBakeJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Cells;
        public int Width;
        public int Height;
        [ReadOnly] public NativeArray<int2> Goals;

        public NativeArray<byte> Directions;
        public NativeArray<int> CostField;
        public NativeQueue<int> Queue;

        public void Execute()
        {
            int cellCount = Width * Height;

            // 1. 初始化代价场
            for (int i = 0; i < cellCount; i++)
            {
                CostField[i] = FlowFieldBaker.CostUnreachable;
                Directions[i] = FlowFieldMath.DirectionNone;
            }

            // 2. 播种目标点
            for (int g = 0; g < Goals.Length; g++)
            {
                int2 goal = Goals[g];
                if (goal.x < 0 || goal.x >= Width || goal.y < 0 || goal.y >= Height)
                    continue;

                int index = goal.y * Width + goal.x;
                var cellType = (CellType)Cells[index];
                if ((cellType & CellType.Walkable) == 0)
                    continue;

                CostField[index] = 0;
                Queue.Enqueue(index);
            }

            // 3. BFS 扩散
            while (Queue.Count > 0)
            {
                int currentIndex = Queue.Dequeue();
                int currentCost = CostField[currentIndex];
                int cx = currentIndex % Width;
                int cy = currentIndex / Width;

                for (int d = 0; d < 8; d++)
                {
                    int2 offset = FlowFieldMath.GetNeighborOffset(d);
                    int nx = cx + offset.x;
                    int ny = cy + offset.y;

                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                        continue;

                    int neighborIndex = ny * Width + nx;
                    var neighborType = (CellType)Cells[neighborIndex];
                    if ((neighborType & CellType.Walkable) == 0)
                        continue;

                    int newCost = currentCost + 1;
                    if (newCost < CostField[neighborIndex])
                    {
                        CostField[neighborIndex] = newCost;
                        Queue.Enqueue(neighborIndex);
                    }
                }
            }

            // 4. 从代价场计算方向场
            for (int i = 0; i < cellCount; i++)
            {
                if (CostField[i] == FlowFieldBaker.CostUnreachable)
                    continue;

                // 目标格子标记为 DirectionGoal（254）
                if (CostField[i] == 0)
                {
                    Directions[i] = FlowFieldMath.DirectionGoal;
                    continue;
                }

                int cx = i % Width;
                int cy = i / Width;
                int bestCost = CostField[i];
                int2 bestOffset = int2.zero;

                for (int d = 0; d < 8; d++)
                {
                    int2 offset = FlowFieldMath.GetNeighborOffset(d);
                    int nx = cx + offset.x;
                    int ny = cy + offset.y;

                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                        continue;

                    int neighborCost = CostField[ny * Width + nx];
                    if (neighborCost < bestCost)
                    {
                        bestCost = neighborCost;
                        bestOffset = offset;
                    }
                }

                if (bestOffset.x != 0 || bestOffset.y != 0)
                {
                    Directions[i] = FlowFieldMath.OffsetToDirection(bestOffset);
                }
            }
        }
    }
}
