using System;
using System.Collections.Generic;
using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityAddressIndex
    {
        [MemoryPackOrder(1)]
        public int SchemaVersion { get; set; } = 1;

        [MemoryPackOrder(50)]
        public List<EntityTypeAddressBucket> TypeBuckets { get; set; } = new List<EntityTypeAddressBucket>();

        #region Runtime Lookup Cache

        // 运行时查找缓存，不序列化，反序列化后构建
        [MemoryPackIgnore]
        private Dictionary<(EntityType, int), EntityAddressItem> _itemLookupCache;

        [MemoryPackIgnore]
        private Dictionary<string, (EntityType, int, string)> _addressToKeyCache;

        /// <summary>
        /// 构建运行时查找缓存。应在反序列化后调用。
        /// </summary>
        public void BuildLookupCache()
        {
            _itemLookupCache = new Dictionary<(EntityType, int), EntityAddressItem>();
            _addressToKeyCache = new Dictionary<string, (EntityType, int, string)>(StringComparer.Ordinal);

            foreach (var bucket in TypeBuckets)
            {
                foreach (var item in bucket.Items)
                {
                    var key = (bucket.EntityType, item.LocalId);
                    _itemLookupCache[key] = item;
                    _addressToKeyCache[item.Address] = (bucket.EntityType, item.LocalId, item.EntityIdToken);
                }
            }
        }

        /// <summary>
        /// O(1)快速查找地址
        /// </summary>
        public bool TryGetAddressFast(EntityType entityType, int localId, out string address)
        {
            if (_itemLookupCache != null &&
                _itemLookupCache.TryGetValue((entityType, localId), out var item))
            {
                address = item.Address;
                return true;
            }
            address = null;
            return false;
        }

        /// <summary>
        /// O(1)快速反向查找
        /// </summary>
        public bool TryGetKeyByAddressFast(string address, out EntityType entityType, out int localId, out string entityToken)
        {
            if (_addressToKeyCache != null &&
                _addressToKeyCache.TryGetValue(address, out var value))
            {
                entityType = value.Item1;
                localId = value.Item2;
                entityToken = value.Item3;
                return true;
            }
            entityType = EntityType.TURRET;
            localId = 0;
            entityToken = string.Empty;
            return false;
        }

        /// <summary>
        /// 是否已经构建了查找缓存
        /// </summary>
        [MemoryPackIgnore]
        public bool HasBuiltCache => _itemLookupCache != null;

        #endregion

        public bool TryGetAddress(EntityType entityType, int localId, out string address)
        {
            for (int i = 0; i < TypeBuckets.Count; i++)
            {
                var bucket = TypeBuckets[i];
                if (bucket.EntityType != entityType)
                    continue;

                for (int j = 0; j < bucket.Items.Count; j++)
                {
                    var item = bucket.Items[j];
                    if (item.LocalId == localId)
                    {
                        address = item.Address;
                        return true;
                    }
                }
            }

            address = null;
            return false;
        }

        public bool TryGetKeyByAddress(string address, out EntityType entityType, out int localId, out string entityToken)
        {
            for (int i = 0; i < TypeBuckets.Count; i++)
            {
                var bucket = TypeBuckets[i];
                for (int j = 0; j < bucket.Items.Count; j++)
                {
                    var item = bucket.Items[j];
                    if (!string.Equals(item.Address, address, StringComparison.Ordinal))
                        continue;

                    entityType = bucket.EntityType;
                    localId = item.LocalId;
                    entityToken = item.EntityIdToken;
                    return true;
                }
            }

            entityType = EntityType.TURRET;
            localId = 0;
            entityToken = string.Empty;
            return false;
        }

        public bool TryGetEntityToken(EntityType entityType, int localId, out string token)
        {
            for (int i = 0; i < TypeBuckets.Count; i++)
            {
                var bucket = TypeBuckets[i];
                if (bucket.EntityType != entityType)
                    continue;

                for (int j = 0; j < bucket.Items.Count; j++)
                {
                    if (bucket.Items[j].LocalId == localId)
                    {
                        token = bucket.Items[j].EntityIdToken;
                        return true;
                    }
                }
            }

            token = string.Empty;
            return false;
        }

        public static EntityAddressIndex Empty()
        {
            return new EntityAddressIndex();
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityTypeAddressBucket
    {
        [MemoryPackOrder(1)]
        public EntityType EntityType { get; set; }

        [MemoryPackOrder(50)]
        public List<EntityAddressItem> Items { get; set; } = new List<EntityAddressItem>();
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityAddressItem
    {
        [MemoryPackOrder(1)]
        public int LocalId { get; set; }

        [MemoryPackOrder(2)]
        public string EntityIdToken { get; set; }

        [MemoryPackOrder(3)]
        public string EnumName { get; set; }

        [MemoryPackOrder(50)]
        public string Address { get; set; }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class CommonEntityConfigCompatV1
    {
        [MemoryPackOrder(1)]
        public string EntityIdToken { get; set; }

        [MemoryPackOrder(2)]
        public string Name { get; set; }

        [MemoryPackOrder(3)]
        public string Description { get; set; }

        [MemoryPackOrder(50)]
        public string IconAddress { get; set; }

        [MemoryPackOrder(100)]
        public string EntityBlueprintGuid { get; set; }

        [MemoryPackOrder(200)]
        public int Version { get; set; }
    }

    public interface IEntityConfigPackage
    {
        EntityType EntityType { get; }

        string EntityIdToken { get; set; }

        string EntityBlueprintGuid { get; set; }

        string ExtraSfxAddress { get; set; }

        int Version { get; set; }

        bool IsDirty { get; set; }

        int SchemaVersion { get; set; }

        string DisplayNameForLog { get; }

        void Normalize();
    }

    public readonly struct EntityDataChangeEvent
    {
        public EntityDataChangeEvent(EntityType entityType, int localId, string entityToken, string address,
            int version, bool isDirty, string reason)
        {
            EntityType = entityType;
            LocalId = localId;
            EntityToken = entityToken;
            Address = address;
            Version = version;
            IsDirty = isDirty;
            Reason = reason;
        }

        public EntityType EntityType { get; }

        public int LocalId { get; }

        public string EntityToken { get; }

        public string Address { get; }

        public int Version { get; }

        public bool IsDirty { get; }

        public string Reason { get; }
    }
}

