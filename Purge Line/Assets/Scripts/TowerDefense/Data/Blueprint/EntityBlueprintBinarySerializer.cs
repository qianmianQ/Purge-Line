using System;
using System.IO;
using MemoryPack;

namespace TowerDefense.Data.Blueprint
{
    public static class EntityBlueprintBinarySerializer
    {
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

            byte[] bytes = MemoryPackSerializer.Serialize(document);
            File.WriteAllBytes(filePath, bytes);
        }

        public static EntityBlueprintDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Blueprint file not found", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            return MemoryPackSerializer.Deserialize<EntityBlueprintDocument>(bytes);
        }

        public static EntityBlueprintDocument RoundTrip(EntityBlueprintDocument source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            byte[] bytes = MemoryPackSerializer.Serialize(source);
            return MemoryPackSerializer.Deserialize<EntityBlueprintDocument>(bytes);
        }
    }
}
