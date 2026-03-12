using System;
using TowerDefense.Components;
using TowerDefense.Data;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 关卡配置资产 — ScriptableObject 包装
    ///
    /// 用于在 Unity 编辑器中可视化编辑关卡地图。
    /// 内部数据与 LevelConfig 双向同步，支持 Inspector 编辑和导出为 MemoryPack .bytes。
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevelConfig", menuName = "TowerDefense/Level Config Asset")]
    public class LevelConfigAsset : ScriptableObject
    {
        [Header("关卡信息")]
        [Tooltip("关卡唯一标识符")]
        public string levelId = "level_01";

        [Tooltip("关卡显示名称")]
        public string displayName = "Level 01";

        [TextArea(2, 4)]
        [Tooltip("关卡描述")]
        public string description = "";

        [Tooltip("配置版本号")]
        public int version = 1;

        [Header("地图参数")]
        [Range(1, 500)]
        [Tooltip("地图宽度（格子数）")]
        public int width = 20;

        [Range(1, 500)]
        [Tooltip("地图高度（格子数）")]
        public int height = 15;

        [Range(0.1f, 10f)]
        [Tooltip("单个格子的世界尺寸")]
        public float cellSize = 1.0f;

        [Tooltip("地图原点 X 坐标")]
        public float originX = 0f;

        [Tooltip("地图原点 Y 坐标")]
        public float originY = 0f;

        [Header("格子数据")]
        [HideInInspector]
        public byte[] cells;

        [Header("路径点")]
        [Tooltip("出生点（格子坐标）")]
        public Vector2[] spawnPoints = Array.Empty<Vector2>();

        [Tooltip("目标点（格子坐标）")]
        public Vector2[] goalPoints = Array.Empty<Vector2>();

        [Header("预烘焙流场（可选）")]
        [HideInInspector]
        public byte[] bakedFlowFieldDirections;

        [HideInInspector]
        public uint bakedFlowFieldDataHash;

        [HideInInspector]
        public int bakedFlowFieldVersion;

        // ── 格子数据操作 ──────────────────────────────────────

        /// <summary>确保格子数组尺寸正确</summary>
        public void EnsureCellsArray()
        {
            int expectedLength = width * height;
            if (cells == null || cells.Length != expectedLength)
            {
                var oldCells = cells;
                cells = new byte[expectedLength];

                // 复制旧数据（尽可能保留）
                if (oldCells != null)
                {
                    int copyLength = Math.Min(oldCells.Length, expectedLength);
                    Array.Copy(oldCells, cells, copyLength);
                }
            }
        }

        /// <summary>获取指定坐标的格子类型</summary>
        public CellType GetCellType(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return CellType.Solid;
            EnsureCellsArray();
            return (CellType)cells[y * width + x];
        }

        /// <summary>设置指定坐标的格子类型</summary>
        public void SetCellType(int x, int y, CellType cellType)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            EnsureCellsArray();
            cells[y * width + x] = (byte)cellType;
        }

        /// <summary>填充全部格子为指定类型</summary>
        public void FillAll(CellType cellType)
        {
            EnsureCellsArray();
            byte fill = (byte)cellType;
            for (int i = 0; i < cells.Length; i++)
                cells[i] = fill;
        }

        /// <summary>填充矩形区域</summary>
        public void FillRect(int startX, int startY, int endX, int endY, CellType cellType)
        {
            EnsureCellsArray();
            int minX = Math.Max(0, Math.Min(startX, endX));
            int maxX = Math.Min(width - 1, Math.Max(startX, endX));
            int minY = Math.Max(0, Math.Min(startY, endY));
            int maxY = Math.Min(height - 1, Math.Max(startY, endY));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    cells[y * width + x] = (byte)cellType;
        }

        // ── 与 LevelConfig 转换 ──────────────────────────────

        /// <summary>转换为运行时 LevelConfig</summary>
        public LevelConfig ToLevelConfig()
        {
            EnsureCellsArray();
            var copy = new byte[cells.Length];
            Array.Copy(cells, copy, cells.Length);

            return new LevelConfig
            {
                LevelId = levelId,
                Version = version,
                Width = width,
                Height = height,
                CellSize = cellSize,
                OriginX = originX,
                OriginY = originY,
                Cells = copy,
                SpawnPoints = spawnPoints ?? Array.Empty<Vector2>(),
                GoalPoints = goalPoints ?? Array.Empty<Vector2>(),
                DisplayName = displayName,
                Description = description,
                BakedFlowFieldDirections = bakedFlowFieldDirections,
                BakedFlowFieldDataHash = bakedFlowFieldDataHash,
                BakedFlowFieldVersion = bakedFlowFieldVersion
            };
        }

        /// <summary>从 LevelConfig 导入数据</summary>
        public void FromLevelConfig(LevelConfig config)
        {
            if (config == null) return;

            levelId = config.LevelId;
            version = config.Version;
            width = config.Width;
            height = config.Height;
            cellSize = config.CellSize;
            originX = config.OriginX;
            originY = config.OriginY;
            displayName = config.DisplayName ?? config.LevelId;
            description = config.Description ?? string.Empty;
            spawnPoints = config.SpawnPoints ?? Array.Empty<Vector2>();
            goalPoints = config.GoalPoints ?? Array.Empty<Vector2>();
            bakedFlowFieldDirections = config.BakedFlowFieldDirections;
            bakedFlowFieldDataHash = config.BakedFlowFieldDataHash;
            bakedFlowFieldVersion = config.BakedFlowFieldVersion;

            cells = new byte[config.Cells.Length];
            Array.Copy(config.Cells, cells, config.Cells.Length);
        }

        // ── Unity 生命周期 ────────────────────────────────────

        private void OnValidate()
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            cellSize = Math.Max(0.01f, cellSize);
            EnsureCellsArray();
        }
    }
}

