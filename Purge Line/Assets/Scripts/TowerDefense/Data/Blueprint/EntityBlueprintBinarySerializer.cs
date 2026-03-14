using System;
using System.IO;
using System.Text;

namespace TowerDefense.Data.Blueprint
{
    public static class EntityBlueprintBinarySerializer
    {
        private const string Magic = "EBP1";

        public static void Save(string filePath, EntityBlueprintDocument document)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is empty", nameof(filePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(Magic);
            writer.Write(document.formatVersion);
            writer.Write(document.blueprintName ?? string.Empty);

            writer.Write(document.components.Count);
            for (int i = 0; i < document.components.Count; i++)
            {
                var component = document.components[i];
                writer.Write(component.componentTypeName ?? string.Empty);
                writer.Write(component.category ?? string.Empty);
                writer.Write(component.expanded);

                writer.Write(component.fields.Count);
                for (int j = 0; j < component.fields.Count; j++)
                {
                    var field = component.fields[j];
                    writer.Write(field.fieldPath ?? string.Empty);
                    writer.Write(field.fieldTypeName ?? string.Empty);
                    writer.Write(field.serializedValue ?? string.Empty);
                }
            }
        }

        public static EntityBlueprintDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Blueprint file not found", filePath);

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            string magic = reader.ReadString();
            if (!string.Equals(magic, Magic, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Invalid blueprint header: {magic}");
            }

            int version = reader.ReadInt32();
            if (version <= 0)
            {
                throw new InvalidDataException($"Invalid blueprint version: {version}");
            }

            var document = new EntityBlueprintDocument
            {
                formatVersion = version,
                blueprintName = reader.ReadString()
            };

            int componentCount = reader.ReadInt32();
            for (int i = 0; i < componentCount; i++)
            {
                var component = new ComponentRecord
                {
                    componentTypeName = reader.ReadString(),
                    category = reader.ReadString(),
                    expanded = reader.ReadBoolean()
                };

                int fieldCount = reader.ReadInt32();
                for (int j = 0; j < fieldCount; j++)
                {
                    component.fields.Add(new FieldRecord
                    {
                        fieldPath = reader.ReadString(),
                        fieldTypeName = reader.ReadString(),
                        serializedValue = reader.ReadString()
                    });
                }

                document.components.Add(component);
            }

            return document;
        }

        public static EntityBlueprintDocument RoundTrip(EntityBlueprintDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(source.formatVersion);
                writer.Write(source.blueprintName ?? string.Empty);

                writer.Write(source.components.Count);
                for (int i = 0; i < source.components.Count; i++)
                {
                    var component = source.components[i];
                    writer.Write(component.componentTypeName ?? string.Empty);
                    writer.Write(component.category ?? string.Empty);
                    writer.Write(component.expanded);

                    writer.Write(component.fields.Count);
                    for (int j = 0; j < component.fields.Count; j++)
                    {
                        var field = component.fields[j];
                        writer.Write(field.fieldPath ?? string.Empty);
                        writer.Write(field.fieldTypeName ?? string.Empty);
                        writer.Write(field.serializedValue ?? string.Empty);
                    }
                }
            }

            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            string magic = reader.ReadString();
            if (!string.Equals(magic, Magic, StringComparison.Ordinal))
                throw new InvalidDataException("RoundTrip magic mismatch");

            var document = new EntityBlueprintDocument
            {
                formatVersion = reader.ReadInt32(),
                blueprintName = reader.ReadString()
            };

            int componentCount = reader.ReadInt32();
            for (int i = 0; i < componentCount; i++)
            {
                var component = new ComponentRecord
                {
                    componentTypeName = reader.ReadString(),
                    category = reader.ReadString(),
                    expanded = reader.ReadBoolean()
                };

                int fieldCount = reader.ReadInt32();
                for (int j = 0; j < fieldCount; j++)
                {
                    component.fields.Add(new FieldRecord
                    {
                        fieldPath = reader.ReadString(),
                        fieldTypeName = reader.ReadString(),
                        serializedValue = reader.ReadString()
                    });
                }

                document.components.Add(component);
            }

            return document;
        }
    }
}

