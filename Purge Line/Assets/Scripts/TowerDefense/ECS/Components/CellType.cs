using System;

namespace TowerDefense.Components
{
    /// <summary>
    /// 格子类型标记（Flags 枚举）
    ///
    /// 设计原则：
    /// - 使用位标记实现组合类型，单个 byte 即可表达所有组合
    /// - None = 未初始化 / 空地
    /// - Solid = 实心阻挡，不可通行不可放置
    /// - Walkable = 敌人可通行
    /// - Placeable = 可放置炮塔
    /// - 组合示例：Walkable | Placeable = 既可通行又可放置的多功能格子
    ///
    /// 扩展指南：
    ///   新增类型只需添加新的位标记（如 Water = 1 << 3），
    ///   不影响已有逻辑，所有位操作查询自动兼容。
    /// </summary>
    [Flags]
    public enum CellType : byte
    {
        /// <summary>未初始化 / 空地</summary>
        None = 0,

        /// <summary>实心格子：不可放置炮塔、不可通行、地面层阻挡</summary>
        Solid = 1 << 0, // 0x01

        /// <summary>通行格子：允许敌人路径寻路通过</summary>
        Walkable = 1 << 1, // 0x02

        /// <summary>放置格子：允许放置炮塔</summary>
        Placeable = 1 << 2, // 0x04

        // ── 预定义组合 ──────────────────────────────
        /// <summary>可通行 + 可放置的多功能格子</summary>
        WalkableAndPlaceable = Walkable | Placeable, // 0x06
    }

    /// <summary>
    /// CellType 扩展方法，提供位操作的便捷查询
    /// </summary>
    public static class CellTypeExtensions
    {
        /// <summary>是否包含指定标记</summary>
        public static bool Has(this CellType self, CellType flag) => (self & flag) == flag;

        /// <summary>是否为实心阻挡格子</summary>
        public static bool IsSolid(this CellType self) => self.Has(CellType.Solid);

        /// <summary>是否允许敌人通行</summary>
        public static bool IsWalkable(this CellType self) => self.Has(CellType.Walkable);

        /// <summary>是否允许放置炮塔</summary>
        public static bool IsPlaceable(this CellType self) => self.Has(CellType.Placeable);
    }
}

