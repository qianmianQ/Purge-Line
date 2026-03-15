using System;
using MemoryPack;
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
    public class LevelConfigAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private byte[] _serializedLevelConfig;

        [NonSerialized]
        private LevelConfig _levelConfig;

        public LevelConfig LevelConfig
        {
            get
            {
                EnsureLevelConfig();
                return _levelConfig;
            }
            set
            {
                _levelConfig = value;
                NormalizeLevelConfig();
            }
        }

        private void EnsureLevelConfig()
        {
            if (_levelConfig == null)
            {
                _levelConfig = LevelConfig.CreateEmpty("level_01", 20, 15);
            }

            NormalizeLevelConfig();
        }

        private void NormalizeLevelConfig()
        {
            if (_levelConfig == null)
                return;

            _levelConfig.LevelId ??= "level_01";
            _levelConfig.Version = Mathf.Max(1, _levelConfig.Version);
            _levelConfig.Width = Mathf.Max(1, _levelConfig.Width);
            _levelConfig.Height = Mathf.Max(1, _levelConfig.Height);
            _levelConfig.CellSize = Mathf.Max(0.01f, _levelConfig.CellSize);
            _levelConfig.DisplayName ??= _levelConfig.LevelId;
            _levelConfig.Description ??= string.Empty;
            _levelConfig.SpawnPoints ??= Array.Empty<Vector2>();
            _levelConfig.GoalPoints ??= Array.Empty<Vector2>();

            EnsureCellsArrayInternal();
        }

        private void EnsureCellsArrayInternal()
        {
            int expectedLength = _levelConfig.Width * _levelConfig.Height;
            if (_levelConfig.Cells == null || _levelConfig.Cells.Length != expectedLength)
            {
                var oldCells = _levelConfig.Cells;
                _levelConfig.Cells = new byte[expectedLength];

                if (oldCells != null)
                {
                    int copyLength = Math.Min(oldCells.Length, expectedLength);
                    Array.Copy(oldCells, _levelConfig.Cells, copyLength);
                }
            }
        }

        // ── 格子数据操作 ──────────────────────────────────────

        /// <summary>确保格子数组尺寸正确</summary>
        public void EnsureCellsArray()
        {
            EnsureLevelConfig();
            EnsureCellsArrayInternal();
        }

        /// <summary>获取指定坐标的格子类型</summary>
        public CellType GetCellType(int x, int y)
        {
            EnsureLevelConfig();
            var config = _levelConfig;
            if (x < 0 || x >= config.Width || y < 0 || y >= config.Height) return CellType.Solid;
            EnsureCellsArrayInternal();
            return (CellType)config.Cells[y * config.Width + x];
        }

        /// <summary>设置指定坐标的格子类型</summary>
        public void SetCellType(int x, int y, CellType cellType)
        {
            EnsureLevelConfig();
            var config = _levelConfig;
            if (x < 0 || x >= config.Width || y < 0 || y >= config.Height) return;
            EnsureCellsArrayInternal();
            config.Cells[y * config.Width + x] = (byte)cellType;
        }

        /// <summary>填充全部格子为指定类型</summary>
        public void FillAll(CellType cellType)
        {
            EnsureLevelConfig();
            var config = _levelConfig;
            EnsureCellsArrayInternal();
            byte fill = (byte)cellType;
            for (int i = 0; i < config.Cells.Length; i++)
                config.Cells[i] = fill;
        }

        /// <summary>填充矩形区域</summary>
        public void FillRect(int startX, int startY, int endX, int endY, CellType cellType)
        {
            EnsureLevelConfig();
            var config = _levelConfig;
            EnsureCellsArrayInternal();
            int minX = Math.Max(0, Math.Min(startX, endX));
            int maxX = Math.Min(config.Width - 1, Math.Max(startX, endX));
            int minY = Math.Max(0, Math.Min(startY, endY));
            int maxY = Math.Min(config.Height - 1, Math.Max(startY, endY));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    config.Cells[y * config.Width + x] = (byte)cellType;
        }

        // ── 与 LevelConfig 转换 ──────────────────────────────

        /// <summary>转换为运行时 LevelConfig</summary>
        public LevelConfig ToLevelConfig()
        {
            EnsureLevelConfig();
            var config = _levelConfig;
            EnsureCellsArrayInternal();
            var copy = new byte[config.Cells.Length];
            Array.Copy(config.Cells, copy, config.Cells.Length);

            Vector2[] spawnPoints = config.SpawnPoints ?? Array.Empty<Vector2>();
            Vector2[] goalPoints = config.GoalPoints ?? Array.Empty<Vector2>();
            var spawnCopy = new Vector2[spawnPoints.Length];
            var goalCopy = new Vector2[goalPoints.Length];
            Array.Copy(spawnPoints, spawnCopy, spawnPoints.Length);
            Array.Copy(goalPoints, goalCopy, goalPoints.Length);

            byte[] bakedDirectionsCopy = null;
            if (config.BakedFlowFieldDirections != null)
            {
                bakedDirectionsCopy = new byte[config.BakedFlowFieldDirections.Length];
                Array.Copy(config.BakedFlowFieldDirections, bakedDirectionsCopy, config.BakedFlowFieldDirections.Length);
            }

            return new LevelConfig
            {
                LevelId = config.LevelId,
                Version = config.Version,
                Width = config.Width,
                Height = config.Height,
                CellSize = config.CellSize,
                OriginX = config.OriginX,
                OriginY = config.OriginY,
                Cells = copy,
                SpawnPoints = spawnCopy,
                GoalPoints = goalCopy,
                DisplayName = config.DisplayName,
                Description = config.Description,
                BakedFlowFieldDirections = bakedDirectionsCopy,
                BakedFlowFieldDataHash = config.BakedFlowFieldDataHash,
                BakedFlowFieldVersion = config.BakedFlowFieldVersion
            };
        }

        /// <summary>从 LevelConfig 导入数据</summary>
        public void FromLevelConfig(LevelConfig config)
        {
            if (config == null) return;

            var copy = new LevelConfig
            {
                LevelId = config.LevelId,
                Version = config.Version,
                Width = config.Width,
                Height = config.Height,
                CellSize = config.CellSize,
                OriginX = config.OriginX,
                OriginY = config.OriginY,
                Cells = config.Cells != null ? (byte[])config.Cells.Clone() : null,
                SpawnPoints = config.SpawnPoints != null ? (Vector2[])config.SpawnPoints.Clone() : Array.Empty<Vector2>(),
                GoalPoints = config.GoalPoints != null ? (Vector2[])config.GoalPoints.Clone() : Array.Empty<Vector2>(),
                DisplayName = config.DisplayName ?? config.LevelId,
                Description = config.Description ?? string.Empty,
                BakedFlowFieldDirections = config.BakedFlowFieldDirections != null ? (byte[])config.BakedFlowFieldDirections.Clone() : null,
                BakedFlowFieldDataHash = config.BakedFlowFieldDataHash,
                BakedFlowFieldVersion = config.BakedFlowFieldVersion
            };

            LevelConfig = copy;
            SyncSerializedData();
        }

        public void Save()
        {
            SyncSerializedData();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

        public void OnBeforeSerialize()
        {
            SyncSerializedData();
        }

        public void OnAfterDeserialize()
        {
            if (_serializedLevelConfig == null || _serializedLevelConfig.Length == 0)
            {
                EnsureLevelConfig();
                return;
            }

            try
            {
                _levelConfig = MemoryPackSerializer.Deserialize<LevelConfig>(_serializedLevelConfig);
            }
            catch
            {
                _levelConfig = null;
            }

            EnsureLevelConfig();
        }

        private void SyncSerializedData()
        {
            EnsureLevelConfig();
            _serializedLevelConfig = MemoryPackSerializer.Serialize(_levelConfig);
        }

        // ── Unity 生命周期 ────────────────────────────────────

        private void OnValidate()
        {
            EnsureLevelConfig();
            SyncSerializedData();
        }

        private void OnEnable()
        {
            EnsureLevelConfig();
        }
    }
}

