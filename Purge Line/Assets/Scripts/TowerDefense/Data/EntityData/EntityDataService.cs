using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using MELogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.Data.EntityData
{
    public sealed class EntityDataService : IEntityDataService
    {
        private readonly struct EntityKey : IEquatable<EntityKey>
        {
            public EntityKey(EntityType entityType, int localId)
            {
                EntityType = entityType;
                LocalId = localId;
            }

            public EntityType EntityType { get; }

            public int LocalId { get; }

            public bool Equals(EntityKey other)
            {
                return EntityType == other.EntityType && LocalId == other.LocalId;
            }

            public override bool Equals(object obj)
            {
                return obj is EntityKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)EntityType * 397) ^ LocalId;
                }
            }
        }

        private readonly MELogger _logger;
        // 使用LRU缓存替代普通字典，防止内存无限增长
        private readonly LRUCache<EntityKey, IEntityConfigPackage> _cache;
        private readonly Dictionary<EntityKey, string> _keyToAddress = new Dictionary<EntityKey, string>();
        private readonly Dictionary<string, EntityKey> _addressToKey = new Dictionary<string, EntityKey>(StringComparer.Ordinal);
        private readonly Dictionary<object, EntityKey> _runtimeLookup = new Dictionary<object, EntityKey>();
        private readonly Func<string, UniTask<byte[]>> _bytesLoader;
        private readonly string _indexAddress;

        private EntityAddressIndex _index;
        private bool _isInitialized;

        // 默认缓存容量，可根据项目规模调整
        public const int DEFAULT_CACHE_CAPACITY = 200;

        public EntityDataService()
            : this(DefaultAddressablesBytesLoader, EntityDataAddressRules.IndexAddress, DEFAULT_CACHE_CAPACITY)
        {
        }

        public EntityDataService(Func<string, UniTask<byte[]>> bytesLoader, string indexAddress, int cacheCapacity = DEFAULT_CACHE_CAPACITY)
        {
            _logger = GameLogger.Create("EntityDataService");
            _cache = new LRUCache<EntityKey, IEntityConfigPackage>(cacheCapacity);
            _bytesLoader = bytesLoader ?? throw new ArgumentNullException(nameof(bytesLoader));
            _indexAddress = string.IsNullOrWhiteSpace(indexAddress)
                ? EntityDataAddressRules.IndexAddress
                : indexAddress;
        }

        public event Action<EntityDataChangeEvent> EntityDataChanged;

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
                return;

            _index = await LoadIndexAsync();

            // 构建运行时映射表
            BuildRuntimeMaps(_index);

            // 构建索引查找缓存（O(1)查找）
            _index?.BuildLookupCache();

            _isInitialized = true;

            _logger.LogInformation("[EntityDataService] Initialized. Cache capacity: {Capacity}, Index entries: {IndexCount}",
                _cache.Capacity, _index?.TypeBuckets?.Sum(b => b.Items?.Count ?? 0) ?? 0);
        }

        public async UniTask<TurretConfigPackage> GetTurretAsync(TurretId turretId)
        {
            return await GetTypedAsync(EntityType.TURRET, (int)turretId,
                (token, reason) => TurretConfigPackage.BuildFallback(token, reason),
                package => package as TurretConfigPackage);
        }

        public async UniTask<EnemyConfigPackage> GetEnemyAsync(EnemyId enemyId)
        {
            return await GetTypedAsync(EntityType.ENEMY, (int)enemyId,
                (token, reason) => EnemyConfigPackage.BuildFallback(token, reason),
                package => package as EnemyConfigPackage);
        }

        public async UniTask<ProjectileConfigPackage> GetProjectileAsync(ProjectileId projectileId)
        {
            return await GetTypedAsync(EntityType.PROJECTILE, (int)projectileId,
                (token, reason) => ProjectileConfigPackage.BuildFallback(token, reason),
                package => package as ProjectileConfigPackage);
        }

        public bool TryGetCached(EntityType entityType, int localId, out IEntityConfigPackage package)
        {
            return _cache.TryGetValue(new EntityKey(entityType, localId), out package);
        }

        public void RegisterRuntimeInstance(object instance, EntityType entityType, int localId)
        {
            if (instance == null || localId <= 0)
                return;

            _runtimeLookup[instance] = new EntityKey(entityType, localId);
        }

        public void UnregisterRuntimeInstance(object instance)
        {
            if (instance == null)
                return;

            _runtimeLookup.Remove(instance);
        }

        public bool TryGetEntityDataByInstance(object instance, out EntityType entityType, out int localId,
            out IEntityConfigPackage package)
        {
            entityType = EntityType.TURRET;
            localId = 0;
            package = null;

            if (instance == null)
                return false;

            if (!_runtimeLookup.TryGetValue(instance, out var key))
                return false;

            entityType = key.EntityType;
            localId = key.LocalId;

            return _cache.TryGetValue(key, out package);
        }

        public async UniTask<bool> NotifyHotUpdateByAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            await InitializeAsync();

            if (!_addressToKey.TryGetValue(address, out var key))
                return false;

            var package = await LoadPackageByAddressAsync(key, address);
            package.Version += 1;
            package.IsDirty = true;
            _cache.Set(key, package);
            RaiseChanged(key, address, package, "HotUpdate");
            return true;
        }

        public bool ApplyRuntimeMutation(EntityType entityType, int localId, Action<IEntityConfigPackage> mutator,
            string reason)
        {
            if (mutator == null || localId <= 0)
                return false;

            var key = new EntityKey(entityType, localId);

            if (!_cache.TryGetValue(key, out var package))
                return false;

            mutator.Invoke(package);
            package.IsDirty = true;
            package.Version += 1;

            string address = _keyToAddress.TryGetValue(key, out var value)
                ? value
                : EntityDataAddressRules.BuildEntityConfigAddress(entityType, package.EntityIdToken);

            RaiseChanged(key, address, package, string.IsNullOrWhiteSpace(reason) ? "RuntimeMutation" : reason);
            return true;
        }

        private async UniTask<EntityAddressIndex> LoadIndexAsync()
        {
            byte[] bytes;
            try
            {
                bytes = await _bytesLoader.Invoke(_indexAddress);
                if (bytes == null || bytes.Length == 0)
                {
                    _logger.LogError("[EntityDataService] Index address loaded but bytes are empty: {0}",
                        _indexAddress);
                    return EntityAddressIndex.Empty();
                }

                var index = MemoryPackSerializer.Deserialize<EntityAddressIndex>(bytes);
                if (index == null)
                {
                    _logger.LogError("[EntityDataService] Failed to deserialize entity index: {0}",
                        _indexAddress);
                    return EntityAddressIndex.Empty();
                }

                return index;
            }
            catch (Exception ex)
            {
                _logger.LogError("[EntityDataService] Unable to load index address '{0}': {1}",
                    _indexAddress, ex.Message);
                return EntityAddressIndex.Empty();
            }
        }

        private async UniTask<IEntityConfigPackage> LoadPackageByAddressAsync(EntityKey key, string address)
        {
            byte[] bytes;
            try
            {
                bytes = await _bytesLoader.Invoke(address);
                if (bytes == null || bytes.Length == 0)
                {
                    _logger.LogError("[EntityDataService] Entity package bytes are empty. id={0}, address={1}",
                        key.LocalId, address);
                    return BuildFallback(key.EntityType, ResolveToken(key), "Entity package bytes are empty.");
                }

                if (!EntityConfigCompatibility.TryDeserialize(bytes, key.EntityType, key.LocalId, ResolveToken(key),
                        out var package, out var error))
                {
                    _logger.LogError("[EntityDataService] Deserialize failed. id={0}, address={1}, error={2}",
                        key.LocalId, address, error);
                    return BuildFallback(key.EntityType, ResolveToken(key), error);
                }

                package.Normalize();
                return package;
            }
            catch (Exception ex)
            {
                _logger.LogError("[EntityDataService] Address load failed. id={0}, address={1}, error={2}",
                    key.LocalId, address, ex.Message);
                return BuildFallback(key.EntityType, ResolveToken(key), ex.Message);
            }
        }

        private static async UniTask<byte[]> DefaultAddressablesBytesLoader(string address)
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            try
            {
                var textAsset = await handle.Task;
                return textAsset?.bytes;
            }
            finally
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        private void BuildRuntimeMaps(EntityAddressIndex index)
        {
            _keyToAddress.Clear();
            _addressToKey.Clear();

            if (index == null)
                return;

            // 优先使用索引的缓存构建方法（O(1)查找）
            if (index.HasBuiltCache)
            {
                foreach (var bucket in index.TypeBuckets)
                {
                    foreach (var item in bucket.Items)
                    {
                        if (item.LocalId <= 0 || string.IsNullOrWhiteSpace(item.Address))
                            continue;

                        var key = new EntityKey(bucket.EntityType, item.LocalId);
                        _keyToAddress[key] = item.Address;
                        _addressToKey[item.Address] = key;
                    }
                }
            }
            else
            {
                // 回退到原始方法
                for (int i = 0; i < index.TypeBuckets.Count; i++)
                {
                    var bucket = index.TypeBuckets[i];
                    for (int j = 0; j < bucket.Items.Count; j++)
                    {
                        var item = bucket.Items[j];
                        if (item.LocalId <= 0 || string.IsNullOrWhiteSpace(item.Address))
                            continue;

                        var key = new EntityKey(bucket.EntityType, item.LocalId);
                        _keyToAddress[key] = item.Address;
                        _addressToKey[item.Address] = key;
                    }
                }
            }
        }

        private static IEntityConfigPackage BuildFallback(EntityType entityType, string token, string reason)
        {
            switch (entityType)
            {
                case EntityType.TURRET:
                    return TurretConfigPackage.BuildFallback(token, reason);
                case EntityType.ENEMY:
                    return EnemyConfigPackage.BuildFallback(token, reason);
                default:
                    return ProjectileConfigPackage.BuildFallback(token, reason);
            }
        }

        private string ResolveToken(EntityKey key)
        {
            if (_index != null && _index.TryGetEntityToken(key.EntityType, key.LocalId, out var token))
                return token;
            return $"{key.EntityType}_{key.LocalId}";
        }

        private void RaiseChanged(EntityKey key, string address, IEntityConfigPackage package, string reason)
        {
            var evt = new EntityDataChangeEvent(key.EntityType, key.LocalId, ResolveToken(key),
                address, package.Version, package.IsDirty, reason);
            EntityDataChanged?.Invoke(evt);
        }

        private async UniTask<TPackage> GetTypedAsync<TPackage>(EntityType entityType, int localId,
            Func<string, string, TPackage> fallbackFactory, Func<IEntityConfigPackage, TPackage> cast)
            where TPackage : class, IEntityConfigPackage
        {
            if (localId <= 0)
                return fallbackFactory($"{entityType}_None", "Id is None/0.");

            await InitializeAsync();

            var key = new EntityKey(entityType, localId);

            if (_cache.TryGetValue(key, out var cached))
                return cast(cached) ?? fallbackFactory(ResolveToken(key), "Cached package type mismatch.");

            if (!_keyToAddress.TryGetValue(key, out string address))
            {
                _logger.LogError("[EntityDataService] No address mapping for {0}/{1}", entityType, localId);
                var missing = fallbackFactory(ResolveToken(key), "Address mapping not found in entity index.");
                _cache.Set(key, missing);
                return missing;
            }

            var loaded = await LoadPackageByAddressAsync(key, address);
            _cache.Set(key, loaded);
            return cast(loaded) ?? fallbackFactory(ResolveToken(key), "Loaded package type mismatch.");
        }
    }
}


