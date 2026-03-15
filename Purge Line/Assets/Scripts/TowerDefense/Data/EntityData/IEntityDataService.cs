using System;
using Cysharp.Threading.Tasks;

namespace TowerDefense.Data.EntityData
{
    public interface IEntityDataService
    {
        event Action<EntityDataChangeEvent> EntityDataChanged;

        UniTask InitializeAsync();

        UniTask<TurretConfigPackage> GetTurretAsync(TurretId turretId);

        UniTask<EnemyConfigPackage> GetEnemyAsync(EnemyId enemyId);

        UniTask<ProjectileConfigPackage> GetProjectileAsync(ProjectileId projectileId);

        bool TryGetCached(EntityType entityType, int localId, out IEntityConfigPackage package);

        void RegisterRuntimeInstance(object instance, EntityType entityType, int localId);

        void UnregisterRuntimeInstance(object instance);

        bool TryGetEntityDataByInstance(object instance, out EntityType entityType, out int localId,
            out IEntityConfigPackage package);

        UniTask<bool> NotifyHotUpdateByAddressAsync(string address);

        bool ApplyRuntimeMutation(EntityType entityType, int localId, Action<IEntityConfigPackage> mutator, string reason);
    }
}

