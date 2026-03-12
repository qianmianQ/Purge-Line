using System;
using MemoryPack;
using UnityEngine;

namespace TowerDefense.Data
{
    /// <summary>
    /// 关卡配置数据 — MemoryPack 序列化
    ///
    /// 存储地图的所有静态配置信息，包括格子布局、元数据等。
    /// 由编辑器工具导出为 .bytes 文件，运行时反序列化加载。
    ///
    /// 内存布局：
    ///   Cells 数组使用行优先扁平化存储：index = y * Width + x
    ///   每个元素为 CellType 枚举的 byte 值
    ///
    /// 版本兼容：
    ///   Version 字段用于数据迁移，新版本需要保持向下兼容
    /// </summary>
    [MemoryPackable]
    public partial class LevelConfig
    {
        /// <summary>当前流场算法版本，变更时递增以使旧烘焙数据失效</summary>
        public const int FlowFieldAlgorithmVersion = 1;

        /// <summary>关卡唯一标识符</summary>
        public string LevelId { get; set; }

        /// <summary>配置版本号（用于数据迁移）</summary>
        public int Version { get; set; }

        /// <summary>地图宽度（格子数）</summary>
        public int Width { get; set; }

        /// <summary>地图高度（格子数）</summary>
        public int Height { get; set; }

        /// <summary>单个格子的世界尺寸（正方形边长）</summary>
        public float CellSize { get; set; }

        /// <summary>地图原点 X 坐标（世界空间，左下角）</summary>
        public float OriginX { get; set; }

        /// <summary>地图原点 Y 坐标（世界空间，左下角）</summary>
        public float OriginY { get; set; }

        /// <summary>
        /// 扁平化格子类型数组。
        /// 长度 = Width * Height，索引 = y * Width + x。
        /// 每个 byte 对应 CellType 枚举值。
        /// </summary>
        public byte[] Cells { get; set; }

        /// <summary>敌人出生点（格子坐标，x=列 y=行）</summary>
        public Vector2[] SpawnPoints { get; set; }

        /// <summary>目标点（格子坐标，x=列 y=行）</summary>
        public Vector2[] GoalPoints { get; set; }

        /// <summary>关卡显示名称（可选）</summary>
        public string DisplayName { get; set; }

        /// <summary>关卡描述（可选）</summary>
        public string Description { get; set; }

        // ── 预烘焙流场（可选持久化）───────────────────────────

        /// <summary>预烘焙的流场方向数据（每格一个byte，0-7=方向，255=无方向）</summary>
        public byte[] BakedFlowFieldDirections { get; set; }

        /// <summary>烘焙时的网格+目标点数据哈希（用于失效校验）</summary>
        public uint BakedFlowFieldDataHash { get; set; }

        /// <summary>烘焙时的算法版本号</summary>
        public int BakedFlowFieldVersion { get; set; }

        // ── 工厂方法 ─────────────────────────────────────────

        /// <summary>
        /// 创建空白关卡配置
        /// </summary>
        /// <param name="levelId">关卡ID</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="cellSize">格子尺寸</param>
        /// <param name="defaultCellType">默认格子类型</param>
        public static LevelConfig CreateEmpty(string levelId, int width, int height,
            float cellSize = 1.0f, Components.CellType defaultCellType = Components.CellType.None)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid map dimensions: {width}x{height}");

            var cells = new byte[width * height];
            byte fill = (byte)defaultCellType;
            for (int i = 0; i < cells.Length; i++)
                cells[i] = fill;

            return new LevelConfig
            {
                LevelId = levelId ?? throw new ArgumentNullException(nameof(levelId)),
                Version = 1,
                Width = width,
                Height = height,
                CellSize = cellSize,
                OriginX = 0f,
                OriginY = 0f,
                Cells = cells,
                SpawnPoints = Array.Empty<Vector2>(),
                GoalPoints = Array.Empty<Vector2>(),
                DisplayName = levelId,
                Description = string.Empty,
                BakedFlowFieldDirections = null,
                BakedFlowFieldDataHash = 0,
                BakedFlowFieldVersion = 0
            };
        }

        // ── 便捷方法 ─────────────────────────────────────────

        /// <summary>格子总数</summary>
        public int CellCount => Width * Height;

        /// <summary>
        /// 获取指定坐标的格子类型
        /// </summary>
        public Components.CellType GetCellType(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return Components.CellType.Solid;

            return (Components.CellType)Cells[y * Width + x];
        }

        /// <summary>
        /// 设置指定坐标的格子类型
        /// </summary>
        public void SetCellType(int x, int y, Components.CellType cellType)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;

            Cells[y * Width + x] = (byte)cellType;
        }

        /// <summary>
        /// 验证配置数据完整性
        /// </summary>
        /// <param name="error">错误信息（如果验证失败）</param>
        /// <returns>是否通过验证</returns>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(LevelId))
            {
                error = "LevelId is null or empty";
                return false;
            }

            if (Width <= 0 || Height <= 0)
            {
                error = $"Invalid dimensions: {Width}x{Height}";
                return false;
            }

            if (CellSize <= 0f)
            {
                error = $"Invalid cell size: {CellSize}";
                return false;
            }

            if (Cells == null || Cells.Length != Width * Height)
            {
                error = $"Cells array length mismatch: expected {Width * Height}, got {Cells?.Length ?? 0}";
                return false;
            }

            error = null;
            return true;
        }

        // ── 流场烘焙校验 ─────────────────────────────────────

        /// <summary>
        /// 检查预烘焙流场数据是否有效
        /// </summary>
        /// <returns>烘焙数据是否可直接使用</returns>
        public bool HasValidBakedFlowField()
        {
            if (BakedFlowFieldDirections == null || BakedFlowFieldDirections.Length == 0)
                return false;
            if (BakedFlowFieldDirections.Length != Width * Height)
                return false;
            if (BakedFlowFieldVersion != FlowFieldAlgorithmVersion)
                return false;
            if (BakedFlowFieldDataHash != ComputeFlowFieldDataHash())
                return false;
            return true;
        }

        /// <summary>
        /// 计算网格+目标点数据的 FNV-1a 哈希值，用于烘焙数据校验
        /// </summary>
        public uint ComputeFlowFieldDataHash()
        {
            uint hash = 2166136261u;
            if (Cells != null)
            {
                for (int i = 0; i < Cells.Length; i++)
                {
                    hash ^= Cells[i];
                    hash *= 16777619u;
                }
            }
            if (GoalPoints != null)
            {
                for (int i = 0; i < GoalPoints.Length; i++)
                {
                    // 与 FlowFieldBakeSystem.ComputeDataHash 保持一致：使用 int 转换
                    hash ^= (uint)(int)GoalPoints[i].x;
                    hash *= 16777619u;
                    hash ^= (uint)(int)GoalPoints[i].y;
                    hash *= 16777619u;
                }
            }
            return hash;
        }
    }
}

