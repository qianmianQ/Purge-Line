using System;
using MemoryPack;
using NUnit.Framework;
using TowerDefense.Components;
using TowerDefense.Data;
using UnityEngine;

namespace TowerDefense.Tests
{
    /// <summary>
    /// LevelConfig MemoryPack 序列化测试
    ///
    /// 覆盖：
    /// - 基础往返序列化
    /// - 所有格子类型往返
    /// - 大地图往返
    /// - 元数据保持
    /// - 边界情况
    /// - 数据验证
    /// </summary>
    [TestFixture]
    public class LevelConfigSerializationTests
    {
        // ── 基础往返 ──────────────────────────────────────────

        [Test]
        public void RoundTrip_BasicConfig_DataPreserved()
        {
            var original = LevelConfig.CreateEmpty("test_01", 10, 8, 1.5f, CellType.Walkable);
            original.OriginX = 5f;
            original.OriginY = 3f;
            original.DisplayName = "Test Level";
            original.Description = "A test level";

            byte[] bytes = MemoryPackSerializer.Serialize(original);
            var deserialized = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.LevelId, deserialized.LevelId);
            Assert.AreEqual(original.Version, deserialized.Version);
            Assert.AreEqual(original.Width, deserialized.Width);
            Assert.AreEqual(original.Height, deserialized.Height);
            Assert.AreEqual(original.CellSize, deserialized.CellSize, 0.001f);
            Assert.AreEqual(original.OriginX, deserialized.OriginX, 0.001f);
            Assert.AreEqual(original.OriginY, deserialized.OriginY, 0.001f);
            Assert.AreEqual(original.DisplayName, deserialized.DisplayName);
            Assert.AreEqual(original.Description, deserialized.Description);
        }

        [Test]
        public void RoundTrip_AllCellTypes_PreservedCorrectly()
        {
            var config = LevelConfig.CreateEmpty("cell_types", 5, 1);

            // 设置每种格子类型
            config.Cells[0] = (byte)CellType.None;
            config.Cells[1] = (byte)CellType.Solid;
            config.Cells[2] = (byte)CellType.Walkable;
            config.Cells[3] = (byte)CellType.Placeable;
            config.Cells[4] = (byte)CellType.WalkableAndPlaceable;

            byte[] bytes = MemoryPackSerializer.Serialize(config);
            var result = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

            Assert.AreEqual(CellType.None, (CellType)result.Cells[0]);
            Assert.AreEqual(CellType.Solid, (CellType)result.Cells[1]);
            Assert.AreEqual(CellType.Walkable, (CellType)result.Cells[2]);
            Assert.AreEqual(CellType.Placeable, (CellType)result.Cells[3]);
            Assert.AreEqual(CellType.WalkableAndPlaceable, (CellType)result.Cells[4]);
        }

        [Test]
        public void RoundTrip_LargeMap_200x200()
        {
            var config = LevelConfig.CreateEmpty("large_map", 200, 200, 1.0f, CellType.Walkable);

            // 设置一些混合数据
            var random = new System.Random(42);
            for (int i = 0; i < config.Cells.Length; i++)
            {
                config.Cells[i] = (byte)(random.Next(0, 5) switch
                {
                    0 => CellType.None,
                    1 => CellType.Solid,
                    2 => CellType.Walkable,
                    3 => CellType.Placeable,
                    4 => CellType.WalkableAndPlaceable,
                    _ => CellType.None
                });
            }

            byte[] bytes = MemoryPackSerializer.Serialize(config);
            var result = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

            Assert.AreEqual(200, result.Width);
            Assert.AreEqual(200, result.Height);
            Assert.AreEqual(40000, result.Cells.Length);

            // 验证所有格子数据一致
            for (int i = 0; i < config.Cells.Length; i++)
            {
                Assert.AreEqual(config.Cells[i], result.Cells[i],
                    $"Cell mismatch at index {i}");
            }
        }

        // ── 元数据 ────────────────────────────────────────────

        [Test]
        public void RoundTrip_SpawnAndGoalPoints_Preserved()
        {
            var config = LevelConfig.CreateEmpty("points_test", 10, 10);
            config.SpawnPoints = new[] { new Vector2(0, 0), new Vector2(5, 0) };
            config.GoalPoints = new[] { new Vector2(9, 9) };

            byte[] bytes = MemoryPackSerializer.Serialize(config);
            var result = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

            Assert.AreEqual(2, result.SpawnPoints.Length);
            Assert.AreEqual(new Vector2(0, 0), result.SpawnPoints[0]);
            Assert.AreEqual(new Vector2(5, 0), result.SpawnPoints[1]);
            Assert.AreEqual(1, result.GoalPoints.Length);
            Assert.AreEqual(new Vector2(9, 9), result.GoalPoints[0]);
        }

        [Test]
        public void RoundTrip_EmptyArrays_Preserved()
        {
            var config = LevelConfig.CreateEmpty("empty_arrays", 5, 5);
            config.SpawnPoints = Array.Empty<Vector2>();
            config.GoalPoints = Array.Empty<Vector2>();

            byte[] bytes = MemoryPackSerializer.Serialize(config);
            var result = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

            Assert.IsNotNull(result.SpawnPoints);
            Assert.IsNotNull(result.GoalPoints);
            Assert.AreEqual(0, result.SpawnPoints.Length);
            Assert.AreEqual(0, result.GoalPoints.Length);
        }

        [Test]
        public void BakedFlowField_ValidHash_ReturnsTrue()
        {
            var config = LevelConfig.CreateEmpty("bake_test", 5, 5, 1.0f, CellType.Walkable);
            config.GoalPoints = new[] { new Vector2(2, 2) };

            config.BakedFlowFieldDirections = new byte[25];
            config.BakedFlowFieldVersion = LevelConfig.FlowFieldAlgorithmVersion;
            config.BakedFlowFieldDataHash = config.ComputeFlowFieldDataHash();

            Assert.IsTrue(config.HasValidBakedFlowField());
        }

        [Test]
        public void BakedFlowField_WrongVersion_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("bake_test", 5, 5, 1.0f, CellType.Walkable);
            config.GoalPoints = new[] { new Vector2(2, 2) };

            config.BakedFlowFieldDirections = new byte[25];
            config.BakedFlowFieldVersion = 0; // wrong version
            config.BakedFlowFieldDataHash = config.ComputeFlowFieldDataHash();

            Assert.IsFalse(config.HasValidBakedFlowField());
        }

        [Test]
        public void BakedFlowField_DataChanged_HashMismatch()
        {
            var config = LevelConfig.CreateEmpty("bake_test", 5, 5, 1.0f, CellType.Walkable);
            config.GoalPoints = new[] { new Vector2(2, 2) };

            config.BakedFlowFieldDirections = new byte[25];
            config.BakedFlowFieldVersion = LevelConfig.FlowFieldAlgorithmVersion;
            config.BakedFlowFieldDataHash = config.ComputeFlowFieldDataHash();

            // Modify grid data after bake
            config.SetCellType(0, 0, CellType.Solid);

            Assert.IsFalse(config.HasValidBakedFlowField());
        }

        // ── LevelConfig API ──────────────────────────────────

        [Test]
        public void GetCellType_ValidCoord_ReturnsCorrectType()
        {
            var config = LevelConfig.CreateEmpty("api_test", 10, 10);
            config.SetCellType(3, 5, CellType.Placeable);

            Assert.AreEqual(CellType.Placeable, config.GetCellType(3, 5));
        }

        [Test]
        public void GetCellType_OutOfBounds_ReturnsSolid()
        {
            var config = LevelConfig.CreateEmpty("bounds_test", 10, 10);

            Assert.AreEqual(CellType.Solid, config.GetCellType(-1, 0));
            Assert.AreEqual(CellType.Solid, config.GetCellType(0, -1));
            Assert.AreEqual(CellType.Solid, config.GetCellType(10, 0));
            Assert.AreEqual(CellType.Solid, config.GetCellType(0, 10));
        }

        [Test]
        public void SetCellType_OutOfBounds_DoesNotThrow()
        {
            var config = LevelConfig.CreateEmpty("set_bounds_test", 10, 10);

            // 应该不崩溃
            Assert.DoesNotThrow(() => config.SetCellType(-1, 0, CellType.Walkable));
            Assert.DoesNotThrow(() => config.SetCellType(0, -1, CellType.Walkable));
            Assert.DoesNotThrow(() => config.SetCellType(10, 0, CellType.Walkable));
            Assert.DoesNotThrow(() => config.SetCellType(0, 10, CellType.Walkable));
        }

        // ── 验证 ──────────────────────────────────────────────

        [Test]
        public void Validate_ValidConfig_ReturnsTrue()
        {
            var config = LevelConfig.CreateEmpty("valid", 10, 10);
            Assert.IsTrue(config.Validate(out _));
        }

        [Test]
        public void Validate_NullLevelId_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.LevelId = null;
            Assert.IsFalse(config.Validate(out var error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void Validate_EmptyLevelId_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.LevelId = "";
            Assert.IsFalse(config.Validate(out _));
        }

        [Test]
        public void Validate_ZeroWidth_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.Width = 0;
            Assert.IsFalse(config.Validate(out _));
        }

        [Test]
        public void Validate_NegativeHeight_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.Height = -1;
            Assert.IsFalse(config.Validate(out _));
        }

        [Test]
        public void Validate_ZeroCellSize_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.CellSize = 0f;
            Assert.IsFalse(config.Validate(out _));
        }

        [Test]
        public void Validate_MismatchedCellsArray_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.Cells = new byte[50]; // 应该是 100
            Assert.IsFalse(config.Validate(out _));
        }

        [Test]
        public void Validate_NullCells_ReturnsFalse()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.Cells = null;
            Assert.IsFalse(config.Validate(out _));
        }

        // ── 工厂方法 ─────────────────────────────────────────

        [Test]
        public void CreateEmpty_InvalidDimensions_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LevelConfig.CreateEmpty("test", 0, 10));
            Assert.Throws<ArgumentException>(() => LevelConfig.CreateEmpty("test", 10, 0));
            Assert.Throws<ArgumentException>(() => LevelConfig.CreateEmpty("test", -1, 10));
        }

        [Test]
        public void CreateEmpty_NullLevelId_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => LevelConfig.CreateEmpty(null, 10, 10));
        }

        [Test]
        public void CreateEmpty_DefaultCellType_FillsCorrectly()
        {
            var config = LevelConfig.CreateEmpty("fill_test", 5, 5, 1.0f, CellType.Placeable);

            for (int i = 0; i < config.Cells.Length; i++)
            {
                Assert.AreEqual((byte)CellType.Placeable, config.Cells[i]);
            }
        }

        [Test]
        public void CellCount_ReturnsWidthTimesHeight()
        {
            var config = LevelConfig.CreateEmpty("count_test", 15, 20);
            Assert.AreEqual(300, config.CellCount);
        }

        // ── 序列化大小 ───────────────────────────────────────

        [Test]
        public void SerializedSize_200x200_UnderThreshold()
        {
            var config = LevelConfig.CreateEmpty("size_test", 200, 200);
            byte[] bytes = MemoryPackSerializer.Serialize(config);

            // 200*200 = 40000 bytes 的 Cells + metadata ≈ < 50KB
            Assert.Less(bytes.Length, 50 * 1024,
                $"Serialized size {bytes.Length} bytes exceeds 50KB threshold");
        }

        // ── LevelConfigLoader API ────────────────────────────

        [Test]
        public void Serialize_ValidConfig_ReturnsBytes()
        {
            var config = LevelConfig.CreateEmpty("serialize_test", 10, 10);
            byte[] bytes = LevelConfigLoader.Serialize(config);

            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 0);
        }

        [Test]
        public void Serialize_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => LevelConfigLoader.Serialize(null));
        }

        [Test]
        public void Serialize_InvalidConfig_ThrowsArgumentException()
        {
            var config = LevelConfig.CreateEmpty("test", 10, 10);
            config.Width = 0; // 使其无效
            Assert.Throws<ArgumentException>(() => LevelConfigLoader.Serialize(config));
        }

        [Test]
        public void LoadFromBytes_ValidData_ReturnsConfig()
        {
            var original = LevelConfig.CreateEmpty("bytes_test", 10, 10, 1.0f, CellType.Walkable);
            byte[] bytes = LevelConfigLoader.Serialize(original);

            var result = LevelConfigLoader.LoadFromBytes(bytes);

            Assert.IsNotNull(result);
            Assert.AreEqual("bytes_test", result.LevelId);
            Assert.AreEqual(10, result.Width);
            Assert.AreEqual(10, result.Height);
        }

        [Test]
        public void LoadFromBytes_NullData_ReturnsNull()
        {
            var result = LevelConfigLoader.LoadFromBytes(null);
            Assert.IsNull(result);
        }

        [Test]
        public void LoadFromBytes_EmptyData_ReturnsNull()
        {
            var result = LevelConfigLoader.LoadFromBytes(Array.Empty<byte>());
            Assert.IsNull(result);
        }
    }
}

