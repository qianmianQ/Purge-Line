using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TowerDefense.Utilities
{
    /// <summary>
    /// 流场数学工具 — Burst 兼容的纯静态方法
    ///
    /// 方向编码约定（8方向，顺时针从北开始）：
    ///   7(NW)  0(N)  1(NE)
    ///   6(W)    X    2(E)
    ///   5(SW)  4(S)  3(SE)
    ///   255 = 无方向（目标格/不可通行/未计算）
    /// </summary>
    public static class FlowFieldMath
    {
        /// <summary>无方向标记值（不可通行/不可达）</summary>
        public const byte DirectionNone = 255;

        /// <summary>目标格子标记值（到达目标）</summary>
        public const byte DirectionGoal = 254;

        /// <summary>有效方向数量</summary>
        public const int DirectionCount = 8;

        /// <summary>
        /// 方向索引转格子偏移量
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 DirectionToOffset(byte direction)
        {
            switch (direction)
            {
                case 0: return new int2(0, 1);    // N
                case 1: return new int2(1, 1);    // NE
                case 2: return new int2(1, 0);    // E
                case 3: return new int2(1, -1);   // SE
                case 4: return new int2(0, -1);   // S
                case 5: return new int2(-1, -1);  // SW
                case 6: return new int2(-1, 0);   // W
                case 7: return new int2(-1, 1);   // NW
                default: return int2.zero;
            }
        }

        /// <summary>
        /// 方向索引转归一化浮点向量（用于移动）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DirectionToVector(byte direction)
        {
            const float D = 0.70710678f; // 1 / sqrt(2)
            switch (direction)
            {
                case 0: return new float2(0f, 1f);
                case 1: return new float2(D, D);
                case 2: return new float2(1f, 0f);
                case 3: return new float2(D, -D);
                case 4: return new float2(0f, -1f);
                case 5: return new float2(-D, -D);
                case 6: return new float2(-1f, 0f);
                case 7: return new float2(-D, D);
                default: return float2.zero;
            }
        }

        /// <summary>
        /// 格子偏移量转方向索引
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte OffsetToDirection(int2 offset)
        {
            // 快速查表：编码为 (dx+1)*3 + (dy+1)
            int key = (offset.x + 1) * 3 + (offset.y + 1);
            switch (key)
            {
                case 1: return 6; // (-1, 0) = W
                case 0: return 5; // (-1,-1) = SW
                case 2: return 7; // (-1, 1) = NW
                case 4: return 4; // ( 0,-1) = S
                case 5: return 0; // ( 0, 1) = N
                case 7: return 3; // ( 1,-1) = SE
                case 8: return 1; // ( 1, 1) = NE
                case 6: return 2; // ( 1, 0) = E
                default: return DirectionNone;
            }
        }

        /// <summary>
        /// 获取第 i 个邻居偏移（i = 0..7）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetNeighborOffset(int i)
        {
            switch (i)
            {
                case 0: return new int2(0, 1);
                case 1: return new int2(1, 1);
                case 2: return new int2(1, 0);
                case 3: return new int2(1, -1);
                case 4: return new int2(0, -1);
                case 5: return new int2(-1, -1);
                case 6: return new int2(-1, 0);
                case 7: return new int2(-1, 1);
                default: return int2.zero;
            }
        }

        /// <summary>
        /// 检查方向是否有效（0-7）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidDirection(byte direction)
        {
            return direction < DirectionCount;
        }

        /// <summary>
        /// 检查方向是否为目标格子
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGoalDirection(byte direction)
        {
            return direction == DirectionGoal;
        }
    }
}
