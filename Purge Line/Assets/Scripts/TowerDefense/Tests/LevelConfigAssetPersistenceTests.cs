#if UNITY_EDITOR
using NUnit.Framework;
using TowerDefense.Components;
using TowerDefense.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Tests
{
    [TestFixture]
    public class LevelConfigAssetPersistenceTests
    {
        private const string TestAssetPath = "Assets/__Temp_LevelConfigAsset_Test.asset";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestAssetPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Save_PersistsLevelConfigPayload()
        {
            var asset = ScriptableObject.CreateInstance<LevelConfigAsset>();
            asset.LevelConfig.LevelId = "save_test";
            asset.LevelConfig.Width = 8;
            asset.LevelConfig.Height = 6;
            asset.EnsureCellsArray();
            asset.SetCellType(2, 3, CellType.Placeable);

            AssetDatabase.CreateAsset(asset, TestAssetPath);
            asset.Save();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<LevelConfigAsset>(TestAssetPath);
            Assert.IsNotNull(reloaded);
            Assert.IsNotNull(reloaded.LevelConfig);
            Assert.AreEqual("save_test", reloaded.LevelConfig.LevelId);
            Assert.AreEqual(8, reloaded.LevelConfig.Width);
            Assert.AreEqual(6, reloaded.LevelConfig.Height);
            Assert.AreEqual((byte)CellType.Placeable, reloaded.LevelConfig.Cells[3 * 8 + 2]);
        }
    }
}
#endif

