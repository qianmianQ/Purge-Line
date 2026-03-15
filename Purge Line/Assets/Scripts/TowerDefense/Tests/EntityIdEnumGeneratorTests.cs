#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using TowerDefense.Data.EntityData;
using TowerDefense.Editor.EntityData;

namespace TowerDefense.Tests
{
    [TestFixture]
    public class EntityIdEnumGeneratorTests
    {
        [Test]
        public void Generate_WritesPerTypeEnums_WithMax()
        {
            var registry = UnityEngine.ScriptableObject.CreateInstance<EntityConfigRegistryAsset>();
            registry.MutableRecords.Add(new EntityConfigRegistryRecord { EntityType = EntityType.TURRET, EntityIdToken = "TURRET_WIND_SENTRY" });
            registry.MutableRecords.Add(new EntityConfigRegistryRecord { EntityType = EntityType.TURRET, EntityIdToken = "TURRET_WIND_SENTRY" });
            registry.MutableRecords.Add(new EntityConfigRegistryRecord { EntityType = EntityType.ENEMY, EntityIdToken = "ENEMY_GOBLIN" });

            EntityIdEnumGenerator.Generate(registry);
            string content = File.ReadAllText(Path.GetFullPath(EntityIdEnumGenerator.OutputPath));

            StringAssert.Contains("enum TurretId", content);
            StringAssert.Contains("enum EnemyId", content);
            StringAssert.Contains("enum ProjectileId", content);
            StringAssert.Contains("None = 0", content);
            StringAssert.Contains("Max =", content);
            StringAssert.Contains("ENEMY_GOBLIN = 1", content);
            StringAssert.Contains("TURRET_WIND_SENTRY", content);
            StringAssert.Contains("TURRET_WIND_SENTRY_2", content);
        }

        [Test]
        public void Sanitize_InvalidName_ReturnsSafeIdentifier()
        {
            string value = EntityIdEnumGenerator.Sanitize("12 turret-风");
            Assert.AreEqual("ID_12_TURRET", value);
        }
    }
}
#endif
