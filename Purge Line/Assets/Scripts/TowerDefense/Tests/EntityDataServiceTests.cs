using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MemoryPack;
using NUnit.Framework;
using TowerDefense.Data.EntityData;

namespace TowerDefense.Tests
{
    [TestFixture]
    public class EntityDataServiceTests
    {
        [Test]
        public async Task GetTurretAsync_And_ReverseLookup_Works()
        {
            var storage = BuildStorage();
            var service = new EntityDataService(address =>
            {
                storage.TryGetValue(address, out var bytes);
                return UniTask.FromResult(bytes);
            }, "unit/index");

            TurretConfigPackage package = await service.GetTurretAsync((TurretId)1).AsTask();
            Assert.IsNotNull(package);
            Assert.AreEqual("TURRET_WIND_SENTRY", package.EntityIdToken);

            object runtimeObject = new object();
            service.RegisterRuntimeInstance(runtimeObject, EntityType.TURRET, 1);
            bool ok = service.TryGetEntityDataByInstance(runtimeObject, out var type, out var localId, out var byInstance);

            Assert.IsTrue(ok);
            Assert.AreEqual(EntityType.TURRET, type);
            Assert.AreEqual(1, localId);
            Assert.IsNotNull(byInstance);
            Assert.AreEqual("Wind Sentry", byInstance.DisplayNameForLog);
        }

        [Test]
        public async Task RuntimeMutation_TriggersChangeEvent()
        {
            var storage = BuildStorage();
            var service = new EntityDataService(address =>
            {
                storage.TryGetValue(address, out var bytes);
                return UniTask.FromResult(bytes);
            }, "unit/index");

            var events = new List<EntityDataChangeEvent>();
            service.EntityDataChanged += evt => events.Add(evt);

            await service.GetTurretAsync((TurretId)1).AsTask();
            bool changed = service.ApplyRuntimeMutation(EntityType.TURRET, 1, pkg => pkg.IsDirty = true, "BuffApplied");

            Assert.IsTrue(changed);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(EntityType.TURRET, events[0].EntityType);
            Assert.AreEqual(1, events[0].LocalId);
            Assert.AreEqual("BuffApplied", events[0].Reason);
        }

        [Test]
        public async Task NotifyHotUpdate_ReloadsAndRaisesEvent()
        {
            var storage = BuildStorage();
            var service = new EntityDataService(address =>
            {
                storage.TryGetValue(address, out var bytes);
                return UniTask.FromResult(bytes);
            }, "unit/index");

            var events = new List<EntityDataChangeEvent>();
            service.EntityDataChanged += evt => events.Add(evt);

            await service.GetTurretAsync((TurretId)1).AsTask();
            storage["unit/turret/wind_sentry"] = MemoryPackSerializer.Serialize(CreateTurret("TURRET_WIND_SENTRY", "Wind Sentry Mk2", 25));

            bool result = await service.NotifyHotUpdateByAddressAsync("unit/turret/wind_sentry").AsTask();
            Assert.IsTrue(result);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, events[0].LocalId);
        }

        private static Dictionary<string, byte[]> BuildStorage()
        {
            var storage = new Dictionary<string, byte[]>();
            var index = new EntityAddressIndex();
            index.TypeBuckets.Add(new EntityTypeAddressBucket
            {
                EntityType = EntityType.TURRET,
                Items =
                {
                    new EntityAddressItem
                    {
                        LocalId = 1,
                        EntityIdToken = "TURRET_WIND_SENTRY",
                        EnumName = "WIND_SENTRY",
                        Address = "unit/turret/wind_sentry"
                    }
                }
            });

            storage["unit/index"] = MemoryPackSerializer.Serialize(index);
            storage["unit/turret/wind_sentry"] = MemoryPackSerializer.Serialize(CreateTurret("TURRET_WIND_SENTRY", "Wind Sentry", 20));
            return storage;
        }

        private static TurretConfigPackage CreateTurret(string token, string name, int cost)
        {
            return new TurretConfigPackage
            {
                EntityIdToken = token,
                Base = new TurretBaseData
                {
                    Name = name,
                    Description = "desc",
                    Cost = cost,
                    MaxHp = 100f,
                    AttackRange = 4f,
                    AttackInterval = 1f
                },
                Ui = new TurretUIData
                {
                    DisplayName = name,
                    Description = "ui-desc",
                    ThemeColorHex = "#FFFFFFFF"
                },
                Version = 1,
                IsDirty = false
            };
        }
    }
}
