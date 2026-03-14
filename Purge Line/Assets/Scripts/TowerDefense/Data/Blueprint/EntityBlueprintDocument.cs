using System;
using System.Collections.Generic;

namespace TowerDefense.Data.Blueprint
{
    [Serializable]
    public sealed class EntityBlueprintDocument
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string blueprintName = "NewEntityBlueprint";
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

    [Serializable]
    public sealed class ComponentRecord
    {
        public string componentTypeName;
        public string category;
        public bool expanded = true;
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

    [Serializable]
    public sealed class FieldRecord
    {
        public string fieldPath;
        public string fieldTypeName;
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

