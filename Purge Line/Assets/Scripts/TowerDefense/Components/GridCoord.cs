using Unity.Entities;
using Unity.Mathematics;

namespace TowerDefense.Components
{
    /// <summary>
    /// 格子坐标 — IComponentData
    ///
    /// 挂载在需要与网格关联的实体上（炮塔、建筑等），
    /// 表示该实体所占据的格子坐标位置。
    /// </summary>
    public struct GridCoord : IComponentData
    {
        /// <summary>格子列坐标 (0-based)</summary>
        public int X;

        /// <summary>格子行坐标 (0-based)</summary>
        public int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridCoord(int2 coord)
        {
            X = coord.x;
            Y = coord.y;
        }

        /// <summary>转换为 int2</summary>
        public int2 ToInt2() => new int2(X, Y);

        /// <summary>从 int2 隐式转换</summary>
        public static implicit operator GridCoord(int2 v) => new GridCoord(v);

        /// <summary>向 int2 隐式转换</summary>
        public static implicit operator int2(GridCoord c) => new int2(c.X, c.Y);

        public override string ToString() => $"GridCoord({X}, {Y})";
    }
}

