using System;
using System.Collections.Generic;
using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityAddressIndex
    {
        private enum FieldOrder : ushort
        {
            SchemaVersion = 1,
            TypeBuckets = 50
        }

        [MemoryPackOrder((ushort)FieldOrder.SchemaVersion)]
        public int SchemaVersion { get; set; } = 1;

        [MemoryPackOrder((ushort)FieldOrder.TypeBuckets)]
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
        private enum FieldOrder : ushort
        {
            EntityType = 1,
            Items = 50
        }

        [MemoryPackOrder((ushort)FieldOrder.EntityType)]
        public EntityType EntityType { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Items)]
        public List<EntityAddressItem> Items { get; set; } = new List<EntityAddressItem>();
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityAddressItem
    {
        private enum FieldOrder : ushort
        {
            LocalId = 1,
            EntityIdToken = 2,
            EnumName = 3,
            Address = 50
        }

        [MemoryPackOrder((ushort)FieldOrder.LocalId)]
        public int LocalId { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.EntityIdToken)]
        public string EntityIdToken { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.EnumName)]
        public string EnumName { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Address)]
        public string Address { get; set; }
    }

    public interface IEntityConfigPackage
    {
        EntityType EntityType { get; }

        string EntityIdToken { get; set; }

        string EntityBlueprintAddress { get; set; }

        string CompiledBlueprintAddress { get; set; }

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
