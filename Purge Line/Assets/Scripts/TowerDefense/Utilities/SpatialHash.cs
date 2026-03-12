using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace TowerDefense.Utilities
{
    /// <summary>
    /// 空间哈希工具 — Burst 兼容的高性能空间查询
    ///
    /// 将 2D 空间按格子分桶，用于加速范围内实体查询。
    /// 核心场景：炮塔搜索攻击范围内的敌人。
    ///
    /// 性能：10万敌人 + 100炮塔 场景下，搜敌从 O(N×M) 降至 O(N + M×K)
    ///       N=敌人数, M=炮塔数, K=攻击范围覆盖的格子数
    /// </summary>
    [BurstCompile]
    public static class SpatialHash
    {
        /// <summary>空间哈希格子大小（应 >= 最大攻击范围）</summary>
        public const float CellSize = 4.0f;

        /// <summary>
        /// 计算世界坐标对应的哈希 Key
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Hash(float2 position)
        {
            int x = (int)math.floor(position.x / CellSize);
            int y = (int)math.floor(position.y / CellSize);
            // 使用质数混合避免冲突
            return x * 73856093 ^ y * 19349663;
        }

        /// <summary>
        /// 计算指定格子坐标的哈希 Key
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashCell(int cellX, int cellY)
        {
            return cellX * 73856093 ^ cellY * 19349663;
        }

        /// <summary>
        /// 获取世界坐标所在的格子坐标
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetCell(float2 position, out int cellX, out int cellY)
        {
            cellX = (int)math.floor(position.x / CellSize);
            cellY = (int)math.floor(position.y / CellSize);
        }

        /// <summary>
        /// 计算范围查询需要遍历的格子范围
        /// </summary>
        /// <param name="center">查询中心</param>
        /// <param name="radius">查询半径</param>
        /// <param name="minCellX">最小格子X</param>
        /// <param name="minCellY">最小格子Y</param>
        /// <param name="maxCellX">最大格子X</param>
        /// <param name="maxCellY">最大格子Y</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetQueryRange(float2 center, float radius,
            out int minCellX, out int minCellY, out int maxCellX, out int maxCellY)
        {
            minCellX = (int)math.floor((center.x - radius) / CellSize);
            minCellY = (int)math.floor((center.y - radius) / CellSize);
            maxCellX = (int)math.floor((center.x + radius) / CellSize);
            maxCellY = (int)math.floor((center.y + radius) / CellSize);
        }
    }

    /// <summary>
    /// 空间哈希中存储的敌人信息（紧凑结构，减少缓存未命中）
    /// </summary>
    public struct SpatialEnemyInfo
    {
        /// <summary>敌人实体</summary>
        public Unity.Entities.Entity Entity;

        /// <summary>世界坐标</summary>
        public float2 Position;
    }
}

