using System;
using System.Collections.Generic;
using MemoryPack;

namespace TowerDefense.Data.Blueprint
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EntityBlueprintDocument
    {
        private enum FieldOrder : ushort
        {
            FormatVersion = 1,
            BlueprintName = 2,
            Components = 50
        }

        public const int CurrentFormatVersion = 1;

        [MemoryPackOrder((ushort)FieldOrder.FormatVersion)]
        public int formatVersion = CurrentFormatVersion;

        [MemoryPackOrder((ushort)FieldOrder.BlueprintName)]
        public string blueprintName = "NewEntityBlueprint";

        [MemoryPackOrder((ushort)FieldOrder.Components)]
        public List<ComponentRecord> components = new List<ComponentRecord>();

        public EntityBlueprintDocument Clone()
        {
            var clone = new EntityBlueprintDocument
            {
                formatVersion = formatVersion,
                blueprintName = blueprintName
            };

            for (int i = 0; i < components.Count; i++)
            {
                clone.components.Add(components[i].Clone());
            }

            return clone;
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ComponentRecord
    {
        private enum FieldOrder : ushort
        {
            ComponentTypeName = 1,
            Category = 2,
            Expanded = 3,
            Fields = 50
        }

        [MemoryPackOrder((ushort)FieldOrder.ComponentTypeName)]
        public string componentTypeName;

        [MemoryPackOrder((ushort)FieldOrder.Category)]
        public string category;

        [MemoryPackOrder((ushort)FieldOrder.Expanded)]
        public bool expanded = true;

        [MemoryPackOrder((ushort)FieldOrder.Fields)]
        public List<FieldRecord> fields = new List<FieldRecord>();

        public ComponentRecord Clone()
        {
            var clone = new ComponentRecord
            {
                componentTypeName = componentTypeName,
                category = category,
                expanded = expanded
            };

            for (int i = 0; i < fields.Count; i++)
            {
                clone.fields.Add(fields[i].Clone());
            }

            return clone;
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class FieldRecord
    {
        private enum FieldOrder : ushort
        {
            FieldPath = 1,
            FieldTypeName = 2,
            SerializedValue = 3
        }

        [MemoryPackOrder((ushort)FieldOrder.FieldPath)]
        public string fieldPath;

        [MemoryPackOrder((ushort)FieldOrder.FieldTypeName)]
        public string fieldTypeName;

        [MemoryPackOrder((ushort)FieldOrder.SerializedValue)]
        public string serializedValue;

        public FieldRecord Clone()
        {
            return new FieldRecord
            {
                fieldPath = fieldPath,
                fieldTypeName = fieldTypeName,
                serializedValue = serializedValue
            };
        }
    }
}
