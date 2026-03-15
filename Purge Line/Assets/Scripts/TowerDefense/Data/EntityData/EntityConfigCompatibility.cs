using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    public static class EntityConfigCompatibility
    {
        public static bool TryDeserialize(byte[] bytes, EntityType entityType, int localId, string token,
            out IEntityConfigPackage package, out string error)
        {
            package = null;
            error = string.Empty;

            try
            {
                package = DeserializeByType(bytes, entityType);
                if (package == null)
                {
                    error = "Deserialize typed package returned null.";
                    return false;
                }

                if (string.IsNullOrEmpty(package.EntityIdToken))
                    package.EntityIdToken = string.IsNullOrWhiteSpace(token) ? localId.ToString() : token;
                package.Normalize();
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static IEntityConfigPackage DeserializeByType(byte[] bytes, EntityType entityType)
        {
            switch (entityType)
            {
                case EntityType.TURRET:
                    return MemoryPackSerializer.Deserialize<TurretConfigPackage>(bytes);
                case EntityType.ENEMY:
                    return MemoryPackSerializer.Deserialize<EnemyConfigPackage>(bytes);
                case EntityType.PROJECTILE:
                    return MemoryPackSerializer.Deserialize<ProjectileConfigPackage>(bytes);
                default:
                    return null;
            }
        }

    }
}

