using NUnit.Framework;
using TowerDefense.Components;
using TowerDefense.Utilities;
using Unity.Mathematics;

namespace TowerDefense.Tests
{
    /// <summary>
    /// GridMath 纯函数单元测试
    ///
    /// 覆盖所有坐标转换、边界检查、距离计算等核心数学函数。
    /// 每个函数至少覆盖：正常情况、边界情况、异常输入。
    /// </summary>
    [TestFixture]
    public class GridMathTests
    {
        // ── WorldToGrid 测试 ──────────────────────────────────

        [Test]
        public void WorldToGrid_CenterOfFirstCell_ReturnsZeroZero()
        {
            float2 origin = float2.zero;
            float cellSize = 1.0f;
            float2 worldPos = new float2(0.5f, 0.5f);

            var result = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(0, result.x);
            Assert.AreEqual(0, result.y);
        }

        [Test]
        public void WorldToGrid_ExactCellBoundary_ReturnsCorrectCell()
        {
            float2 origin = float2.zero;
            float cellSize = 1.0f;
            float2 worldPos = new float2(1.0f, 1.0f); // 恰好在 (1,1) 格子边界上

            var result = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(1, result.x);
            Assert.AreEqual(1, result.y);
        }

        [Test]
        public void WorldToGrid_WithOffset_ReturnsCorrectCell()
        {
            float2 origin = new float2(5.0f, 3.0f);
            float cellSize = 2.0f;
            float2 worldPos = new float2(8.5f, 6.5f); // (8.5-5)/2 = 1.75 → 1, (6.5-3)/2 = 1.75 → 1

            var result = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(1, result.x);
            Assert.AreEqual(1, result.y);
        }

        [Test]
        public void WorldToGrid_NegativeCoord_ReturnsNegativeResult()
        {
            float2 origin = float2.zero;
            float cellSize = 1.0f;
            float2 worldPos = new float2(-0.5f, -0.5f);

            var result = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(-1, result.x);
            Assert.AreEqual(-1, result.y);
        }

        [Test]
        public void WorldToGrid_LargeCellSize_ReturnsCorrectCell()
        {
            float2 origin = float2.zero;
            float cellSize = 5.0f;
            float2 worldPos = new float2(12.5f, 7.3f); // 12.5/5=2.5→2, 7.3/5=1.46→1

            var result = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(2, result.x);
            Assert.AreEqual(1, result.y);
        }

        // ── GridToWorld 测试 ──────────────────────────────────

        [Test]
        public void GridToWorld_ZeroZero_ReturnsCellCenter()
        {
            float2 origin = float2.zero;
            float cellSize = 1.0f;
            int2 gridCoord = new int2(0, 0);

            var result = GridMath.GridToWorld(gridCoord, origin, cellSize);

            Assert.AreEqual(0.5f, result.x, 0.001f);
            Assert.AreEqual(0.5f, result.y, 0.001f);
        }

        [Test]
        public void GridToWorld_WithOffset_ReturnsCellCenter()
        {
            float2 origin = new float2(10f, 20f);
            float cellSize = 2.0f;
            int2 gridCoord = new int2(3, 2);

            var result = GridMath.GridToWorld(gridCoord, origin, cellSize);

            // 10 + 3*2 + 1 = 17, 20 + 2*2 + 1 = 25
            Assert.AreEqual(17f, result.x, 0.001f);
            Assert.AreEqual(25f, result.y, 0.001f);
        }

        [Test]
        public void GridToWorld_RoundTrip_ReturnsOriginalCell()
        {
            float2 origin = new float2(3.5f, 2.7f);
            float cellSize = 1.5f;
            int2 originalCoord = new int2(5, 8);

            float2 worldPos = GridMath.GridToWorld(originalCoord, origin, cellSize);
            int2 recovered = GridMath.WorldToGrid(worldPos, origin, cellSize);

            Assert.AreEqual(originalCoord.x, recovered.x);
            Assert.AreEqual(originalCoord.y, recovered.y);
        }

        // ── GridToWorldCorner 测试 ────────────────────────────

        [Test]
        public void GridToWorldCorner_ZeroZero_ReturnsOrigin()
        {
            float2 origin = new float2(1f, 2f);
            float cellSize = 1.0f;
            int2 gridCoord = new int2(0, 0);

            var result = GridMath.GridToWorldCorner(gridCoord, origin, cellSize);

            Assert.AreEqual(1f, result.x, 0.001f);
            Assert.AreEqual(2f, result.y, 0.001f);
        }

        // ── GridToIndex 测试 ──────────────────────────────────

        [Test]
        public void GridToIndex_ValidCoord_ReturnsRowMajorIndex()
        {
            int2 coord = new int2(3, 2);
            int width = 10;
            int height = 10;

            int result = GridMath.GridToIndex(coord, width, height);

            Assert.AreEqual(2 * 10 + 3, result); // 23
        }

        [Test]
        public void GridToIndex_Origin_ReturnsZero()
        {
            int2 coord = new int2(0, 0);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void GridToIndex_LastCell_ReturnsMaxIndex()
        {
            int2 coord = new int2(9, 9);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(99, result);
        }

        [Test]
        public void GridToIndex_NegativeX_ReturnsNegativeOne()
        {
            int2 coord = new int2(-1, 0);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void GridToIndex_NegativeY_ReturnsNegativeOne()
        {
            int2 coord = new int2(0, -1);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void GridToIndex_ExceedsWidth_ReturnsNegativeOne()
        {
            int2 coord = new int2(10, 0);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void GridToIndex_ExceedsHeight_ReturnsNegativeOne()
        {
            int2 coord = new int2(0, 10);
            int result = GridMath.GridToIndex(coord, 10, 10);
            Assert.AreEqual(-1, result);
        }

        // ── IndexToGrid 测试 ──────────────────────────────────

        [Test]
        public void IndexToGrid_Zero_ReturnsOrigin()
        {
            var result = GridMath.IndexToGrid(0, 10);
            Assert.AreEqual(0, result.x);
            Assert.AreEqual(0, result.y);
        }

        [Test]
        public void IndexToGrid_ValidIndex_ReturnsCorrectCoord()
        {
            var result = GridMath.IndexToGrid(23, 10);
            Assert.AreEqual(3, result.x);
            Assert.AreEqual(2, result.y);
        }

        [Test]
        public void IndexToGrid_RoundTrip_ReturnsOriginalIndex()
        {
            int width = 15;
            int height = 20;
            int2 original = new int2(7, 13);

            int index = GridMath.GridToIndex(original, width, height);
            int2 recovered = GridMath.IndexToGrid(index, width);

            Assert.AreEqual(original.x, recovered.x);
            Assert.AreEqual(original.y, recovered.y);
        }

        // ── IsInBounds 测试 ───────────────────────────────────

        [Test]
        public void IsInBounds_Origin_ReturnsTrue()
        {
            Assert.IsTrue(GridMath.IsInBounds(new int2(0, 0), 10, 10));
        }

        [Test]
        public void IsInBounds_MaxValid_ReturnsTrue()
        {
            Assert.IsTrue(GridMath.IsInBounds(new int2(9, 9), 10, 10));
        }

        [Test]
        public void IsInBounds_NegativeX_ReturnsFalse()
        {
            Assert.IsFalse(GridMath.IsInBounds(new int2(-1, 0), 10, 10));
        }

        [Test]
        public void IsInBounds_NegativeY_ReturnsFalse()
        {
            Assert.IsFalse(GridMath.IsInBounds(new int2(0, -1), 10, 10));
        }

        [Test]
        public void IsInBounds_ExceedWidth_ReturnsFalse()
        {
            Assert.IsFalse(GridMath.IsInBounds(new int2(10, 0), 10, 10));
        }

        [Test]
        public void IsInBounds_ExceedHeight_ReturnsFalse()
        {
            Assert.IsFalse(GridMath.IsInBounds(new int2(0, 10), 10, 10));
        }

        [Test]
        public void IsInBounds_BothNegative_ReturnsFalse()
        {
            Assert.IsFalse(GridMath.IsInBounds(new int2(-5, -3), 10, 10));
        }

        // ── IsWorldPosInBounds 测试 ───────────────────────────

        [Test]
        public void IsWorldPosInBounds_Inside_ReturnsTrue()
        {
            float2 origin = float2.zero;
            float2 pos = new float2(5.0f, 5.0f);
            Assert.IsTrue(GridMath.IsWorldPosInBounds(pos, origin, 1.0f, 10, 10));
        }

        [Test]
        public void IsWorldPosInBounds_Outside_ReturnsFalse()
        {
            float2 origin = float2.zero;
            float2 pos = new float2(15.0f, 5.0f);
            Assert.IsFalse(GridMath.IsWorldPosInBounds(pos, origin, 1.0f, 10, 10));
        }

        // ── GetNeighbors4 测试 ────────────────────────────────

        [Test]
        public void GetNeighbors4_Center_ReturnsCorrectNeighbors()
        {
            int2 center = new int2(5, 5);

            GridMath.GetNeighbors4(center, out var up, out var down, out var left, out var right);

            Assert.AreEqual(new int2(5, 6), up);
            Assert.AreEqual(new int2(5, 4), down);
            Assert.AreEqual(new int2(4, 5), left);
            Assert.AreEqual(new int2(6, 5), right);
        }

        [Test]
        public void GetNeighbors4_Origin_IncludesNegativeCoords()
        {
            int2 center = new int2(0, 0);

            GridMath.GetNeighbors4(center, out _, out var down, out var left, out _);

            Assert.AreEqual(new int2(0, -1), down);
            Assert.AreEqual(new int2(-1, 0), left);
        }

        // ── GetNeighbors8 测试 ────────────────────────────────

        [Test]
        public void GetNeighbors8_Center_ReturnsAllEightNeighbors()
        {
            int2 center = new int2(5, 5);

            GridMath.GetNeighbors8(center,
                out var n0, out var n1, out var n2, out var n3,
                out var n4, out var n5, out var n6, out var n7);

            Assert.AreEqual(new int2(4, 6), n0); // 左上
            Assert.AreEqual(new int2(5, 6), n1); // 上
            Assert.AreEqual(new int2(6, 6), n2); // 右上
            Assert.AreEqual(new int2(4, 5), n3); // 左
            Assert.AreEqual(new int2(6, 5), n4); // 右
            Assert.AreEqual(new int2(4, 4), n5); // 左下
            Assert.AreEqual(new int2(5, 4), n6); // 下
            Assert.AreEqual(new int2(6, 4), n7); // 右下
        }

        // ── ManhattanDistance 测试 ────────────────────────────

        [Test]
        public void ManhattanDistance_SamePoint_ReturnsZero()
        {
            Assert.AreEqual(0, GridMath.ManhattanDistance(new int2(3, 5), new int2(3, 5)));
        }

        [Test]
        public void ManhattanDistance_Adjacent_ReturnsOne()
        {
            Assert.AreEqual(1, GridMath.ManhattanDistance(new int2(3, 5), new int2(4, 5)));
        }

        [Test]
        public void ManhattanDistance_Diagonal_ReturnsTwo()
        {
            Assert.AreEqual(2, GridMath.ManhattanDistance(new int2(3, 5), new int2(4, 6)));
        }

        [Test]
        public void ManhattanDistance_FarApart_ReturnsCorrectDistance()
        {
            Assert.AreEqual(15, GridMath.ManhattanDistance(new int2(0, 0), new int2(10, 5)));
        }

        // ── ChebyshevDistance 测试 ────────────────────────────

        [Test]
        public void ChebyshevDistance_SamePoint_ReturnsZero()
        {
            Assert.AreEqual(0, GridMath.ChebyshevDistance(new int2(3, 5), new int2(3, 5)));
        }

        [Test]
        public void ChebyshevDistance_Diagonal_ReturnsOne()
        {
            Assert.AreEqual(1, GridMath.ChebyshevDistance(new int2(3, 5), new int2(4, 6)));
        }

        [Test]
        public void ChebyshevDistance_FarApart_ReturnsMaxDimension()
        {
            Assert.AreEqual(10, GridMath.ChebyshevDistance(new int2(0, 0), new int2(10, 5)));
        }

        // ── 综合测试：大规模坐标转换一致性 ─────────────────────

        [Test]
        public void RoundTrip_AllCells_200x200()
        {
            int width = 200;
            int height = 200;
            float cellSize = 1.0f;
            float2 origin = new float2(-100f, -100f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int2 original = new int2(x, y);

                    // Grid → World → Grid 往返
                    float2 world = GridMath.GridToWorld(original, origin, cellSize);
                    int2 recovered = GridMath.WorldToGrid(world, origin, cellSize);

                    Assert.AreEqual(original.x, recovered.x,
                        $"X mismatch at ({x},{y}): world=({world.x},{world.y})");
                    Assert.AreEqual(original.y, recovered.y,
                        $"Y mismatch at ({x},{y}): world=({world.x},{world.y})");

                    // Grid → Index → Grid 往返
                    int index = GridMath.GridToIndex(original, width, height);
                    int2 fromIndex = GridMath.IndexToGrid(index, width);

                    Assert.AreEqual(original.x, fromIndex.x,
                        $"Index roundtrip X mismatch at ({x},{y})");
                    Assert.AreEqual(original.y, fromIndex.y,
                        $"Index roundtrip Y mismatch at ({x},{y})");
                }
            }
        }
    }
}

