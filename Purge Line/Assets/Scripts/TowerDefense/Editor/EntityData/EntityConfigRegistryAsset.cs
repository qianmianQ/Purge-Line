#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TowerDefense.Data.EntityData;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    [CreateAssetMenu(fileName = "EntityConfigRegistry", menuName = "TowerDefense/Entity Data/Registry", order = 10)]
    public sealed class EntityConfigRegistryAsset : ScriptableObject
    {
        [SerializeField] private List<EntityConfigRegistryRecord> _records = new List<EntityConfigRegistryRecord>();

        public IReadOnlyList<EntityConfigRegistryRecord> Records => _records;

        public List<EntityConfigRegistryRecord> MutableRecords => _records;
    }

    [Serializable]
    public sealed class EntityConfigRegistryRecord
    {
        public EntityType EntityType;
        public int LocalId;
        public string DisplayName;
        public string EntityIdToken;
        public string EntityIdEnumName;
        public string Address;
        public string AssetPath;
    }
}
#endif

