using MemoryPack;
using NUnit.Framework;
using TowerDefense.Data.EntityData;

namespace TowerDefense.Tests
{
    [TestFixture]
    public class EntityDataSerializationTests
    {
        [Test]
        public void RoundTrip_TurretConfigPackage_DataPreserved()
        {
            var source = new TurretConfigPackage
            {
                EntityIdToken = "TURRET_WIND_SENTRY",
                Base = new TurretBaseData
                {
                    Name = "Wind Sentry",
                    Description = "Turret",
                    Cost = 12,
                    MaxHp = 80f,
                    AttackRange = 6f,
                    AttackInterval = 0.8f
                },
                Ui = new TurretUIData
                {
                    DisplayName = "Wind Sentry",
                    Description = "ui",
                    IconAddress = "td/ui/sprite/wind_sentry",
                    PreviewAddress = "td/ui/sprite/wind_sentry_preview",
                    ThemeColorHex = "#22FF44FF"
                },
                EntityBlueprintGuid = "guid_001",
                ExtraSfxAddress = "td/audio/turret_spawn",
                Version = 4,
                IsDirty = false
            };

            byte[] bytes = MemoryPackSerializer.Serialize(source);
            var loaded = MemoryPackSerializer.Deserialize<TurretConfigPackage>(bytes);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(source.EntityIdToken, loaded.EntityIdToken);
            Assert.AreEqual(source.Base.AttackRange, loaded.Base.AttackRange);
            Assert.AreEqual(source.Ui.IconAddress, loaded.Ui.IconAddress);
            Assert.AreEqual(source.EntityBlueprintGuid, loaded.EntityBlueprintGuid);
        }

        [Test]
        public void Compatibility_CommonV1_CanMigrateToEnemyPackage()
        {
            var legacy = new CommonEntityConfigCompatV1
            {
                EntityIdToken = "ENEMY_GOBLIN",
                Name = "Goblin",
                Description = "legacy payload",
                IconAddress = "td/ui/sprite/goblin_icon",
                EntityBlueprintGuid = "legacy_guid",
                Version = 2
            };

            byte[] bytes = MemoryPackSerializer.Serialize(legacy);
            bool ok = EntityConfigCompatibility.TryDeserialize(bytes, EntityType.ENEMY, 3, "ENEMY_GOBLIN",
                out var migrated, out var error);

            Assert.IsTrue(ok, error);
            Assert.IsNotNull(migrated);
            Assert.IsInstanceOf<EnemyConfigPackage>(migrated);
            Assert.AreEqual("ENEMY_GOBLIN", migrated.EntityIdToken);
        }

        [Test]
        public void AddressIndex_LookupByTypeAndLocalId_ReturnsExpectedAddress()
        {
            var index = new EntityAddressIndex();
            index.TypeBuckets.Add(new EntityTypeAddressBucket
            {
                EntityType = EntityType.PROJECTILE,
                Items =
                {
                    new EntityAddressItem
                    {
                        LocalId = 2,
                        EntityIdToken = "PROJECTILE_WIND_BOLT",
                        Address = "td/entity/projectile/wind_bolt"
                    }
                }
            });

            bool found = index.TryGetAddress(EntityType.PROJECTILE, 2, out string address);
            Assert.IsTrue(found);
            Assert.AreEqual("td/entity/projectile/wind_bolt", address);
        }
    }
}
