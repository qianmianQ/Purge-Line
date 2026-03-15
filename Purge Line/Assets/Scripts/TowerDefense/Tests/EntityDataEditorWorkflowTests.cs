#if UNITY_EDITOR
using NUnit.Framework;
using TowerDefense.Data.EntityData;
using TowerDefense.Editor.EntityData;

namespace TowerDefense.Tests
{
    [TestFixture]
    public class EntityDataEditorWorkflowTests
    {
        [Test]
        public void TurretEditor_LoadFrom_WritesEditableState()
        {
            var single = UnityEngine.ScriptableObject.CreateInstance<TurretConfigEditorAsset>();
            var package = new TurretConfigPackage
            {
                EntityIdToken = "TURRET_WIND_SENTRY",
                Base = new TurretBaseData { Name = "Wind Sentry", Cost = 12 },
                Ui = new TurretUIData { DisplayName = "Wind Sentry" }
            };

            single.LoadFrom(EntityType.TURRET, 1, "TURRET_WIND_SENTRY", "td/entity/turret/wind_sentry",
                "Assets/Data/EntityData/Configs/turret/wind_sentry.bytes", package);

            Assert.AreEqual(EntityType.TURRET, single.EntityType);
            Assert.AreEqual(1, single.LocalId);
            Assert.AreEqual("TURRET_WIND_SENTRY", single.EntityIdToken);
            Assert.AreEqual("Wind Sentry", single.CurrentConfig.Base.Name);
        }

        [Test]
        public void AddressRules_BuildEntityAddress_IsStable()
        {
            string address = EntityDataAddressRules.BuildEntityConfigAddress(EntityType.PROJECTILE, "wind_bolt");
            Assert.AreEqual("td/entity/projectile/wind_bolt", address);
        }

        [Test]
        public void CreateNameValidation_EmptyName_IsRejected()
        {
            var registry = UnityEngine.ScriptableObject.CreateInstance<EntityConfigRegistryAsset>();
            bool ok = EntityDataEditorUtility.TryValidateCreateName(registry, EntityType.TURRET, "   ", out var message);

            Assert.IsFalse(ok);
            StringAssert.Contains("不能为空", message);
        }

        [Test]
        public void CreateNameValidation_DuplicateNameInSameType_IsRejected()
        {
            var registry = UnityEngine.ScriptableObject.CreateInstance<EntityConfigRegistryAsset>();
            registry.MutableRecords.Add(new EntityConfigRegistryRecord
            {
                EntityType = EntityType.ENEMY,
                DisplayName = "Goblin",
                EntityIdEnumName = "GOBLIN",
                Address = "td/entity/enemy/goblin",
                EntityIdToken = "ENEMY_GOBLIN"
            });

            bool ok = EntityDataEditorUtility.TryValidateCreateName(registry, EntityType.ENEMY, "Goblin", out var message);

            Assert.IsFalse(ok);
            StringAssert.Contains("重复", message);
        }
    }
}
#endif
