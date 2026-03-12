using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Utilities
{
    /// <summary>
    /// 网格数学工具 — 纯静态方法，Burst 兼容
    ///
    /// 所有方法均为纯函数，无状态、无 GC 分配、无托管引用，
    /// 可在 Job 和 Burst 编译上下文中安全调用。
    /// 从 Burst Job 中调用时会被自动内联编译，无需 [BurstCompile] 属性。
    ///
    /// 坐标系统约定：
    /// - 世界坐标 (float2): 连续空间，左下角为 Origin
    /// - 格子坐标 (int2): 离散格子，(0,0) 为左下角
    /// - 数组索引 (int): 行优先扁平化，index = y * width + x
    /// </summary>
    public static class GridMath
    {
        /// <summary>
        /// 世界坐标 → 格子坐标
        /// </summary>
        /// <param name="worldPos">世界空间坐标</param>
        /// <param name="origin">地图原点（左下角世界坐标）</param>
        /// <param name="cellSize">格子尺寸</param>
        /// <param name="result">输出格子坐标（可能超出边界，需配合 IsInBounds 检查）</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WorldToGrid(in float2 worldPos, in float2 origin, float cellSize, out int2 result)
        {
            float2 local = worldPos - origin;
            result = new int2(
                (int)math.floor(local.x / cellSize),
                (int)math.floor(local.y / cellSize)
            );
        }

        /// <summary>
        /// 格子坐标 → 世界坐标（返回格子中心点）
        /// </summary>
        /// <param name="gridCoord">格子坐标</param>
        /// <param name="origin">地图原点</param>
        /// <param name="cellSize">格子尺寸</param>
        /// <param name="result">输出格子中心的世界坐标</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GridToWorld(in int2 gridCoord, in float2 origin, float cellSize, out float2 result)
        {
            result = origin + new float2(
                gridCoord.x * cellSize + cellSize * 0.5f,
                gridCoord.y * cellSize + cellSize * 0.5f
            );
        }

        /// <summary>
        /// 格子坐标 → 世界坐标（返回格子左下角）
        /// </summary>
        /// <param name="result">输出格子左下角的世界坐标</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GridToWorldCorner(in int2 gridCoord, in float2 origin, float cellSize, out float2 result)
        {
            result = origin + new float2(
                gridCoord.x * cellSize,
                gridCoord.y * cellSize
            );
        }

        /// <summary>
        /// 格子坐标 → 扁平数组索引（行优先）
        /// </summary>
        /// <param name="gridCoord">格子坐标</param>
        /// <param name="width">地图宽度</param>
        /// <returns>数组索引，如果超出边界返回 -1</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GridToIndex(in int2 gridCoord, int width, int height)
        {
            if (gridCoord.x < 0 || gridCoord.x >= width ||
                gridCoord.y < 0 || gridCoord.y >= height)
                return -1;

            return gridCoord.y * width + gridCoord.x;
        }

        /// <summary>
        /// 扁平数组索引 → 格子坐标
        /// </summary>
        /// <param name="index">数组索引</param>
        /// <param name="width">地图宽度</param>
        /// <param name="result">输出格子坐标</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToGrid(int index, int width, out int2 result)
        {
            result = new int2(index % width, index / width);
        }

        /// <summary>
        /// 检查格子坐标是否在地图边界内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInBounds(in int2 gridCoord, int width, int height)
        {
            return gridCoord.x >= 0 && gridCoord.x < width &&
                   gridCoord.y >= 0 && gridCoord.y < height;
        }

        /// <summary>
        /// 检查世界坐标是否在地图范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWorldPosInBounds(in float2 worldPos, in float2 origin,
            float cellSize, int width, int height)
        {
            WorldToGrid(worldPos, origin, cellSize, out int2 grid);
            return IsInBounds(grid, width, height);
        }

        /// <summary>
        /// 获取四邻居格子坐标（上下左右），不做边界检查
        /// </summary>
        /// <param name="gridCoord">中心格子坐标</param>
        /// <param name="n0">上</param>
        /// <param name="n1">下</param>
        /// <param name="n2">左</param>
        /// <param name="n3">右</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetNeighbors4(in int2 gridCoord,
            out int2 n0, out int2 n1, out int2 n2, out int2 n3)
        {
            n0 = gridCoord + new int2(0, 1);  // 上
            n1 = gridCoord + new int2(0, -1); // 下
            n2 = gridCoord + new int2(-1, 0); // 左
            n3 = gridCoord + new int2(1, 0);  // 右
        }

        /// <summary>
        /// 获取八邻居格子坐标，不做边界检查
        /// 返回邻居数量（始终为8），结果写入 caller 提供的 buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetNeighbors8(in int2 gridCoord,
            out int2 n0, out int2 n1, out int2 n2, out int2 n3,
            out int2 n4, out int2 n5, out int2 n6, out int2 n7)
        {
            n0 = gridCoord + new int2(-1, 1);  // 左上
            n1 = gridCoord + new int2(0, 1);   // 上
            n2 = gridCoord + new int2(1, 1);   // 右上
            n3 = gridCoord + new int2(-1, 0);  // 左
            n4 = gridCoord + new int2(1, 0);   // 右
            n5 = gridCoord + new int2(-1, -1); // 左下
            n6 = gridCoord + new int2(0, -1);  // 下
            n7 = gridCoord + new int2(1, -1);  // 右下
        }

        /// <summary>
        /// 查询指定格子的 CellType（从 BlobArray 中读取）
        /// 越界返回 Solid（安全默认值，阻止通行和放置）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetCellTypeByte(ref BlobArray<byte> cells,
            in int2 gridCoord, int width, int height)
        {
            int index = GridToIndex(gridCoord, width, height);
            if (index < 0) return (byte)Components.CellType.Solid;
            return cells[index];
        }

        /// <summary>
        /// 计算两个格子坐标之间的曼哈顿距离
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ManhattanDistance(in int2 a, in int2 b)
        {
            int2 diff = math.abs(a - b);
            return diff.x + diff.y;
        }

        /// <summary>
        /// 计算两个格子坐标之间的切比雪夫距离（八方向距离）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChebyshevDistance(in int2 a, in int2 b)
        {
            int2 diff = math.abs(a - b);
            return math.max(diff.x, diff.y);
        }
    }
}

