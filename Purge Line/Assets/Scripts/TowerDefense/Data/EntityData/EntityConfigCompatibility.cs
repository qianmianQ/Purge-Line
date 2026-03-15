using System;
using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    public static class EntityConfigCompatibility
    {
        public static bool TryDeserialize(byte[] bytes, EntityType entityType, int localId, string token,
            out IEntityConfigPackage package, out string error)
        {
            package = null;
            error = null;

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
            catch (Exception ex)
            {
                error = ex.Message;
            }

            try
            {
                var legacy = MemoryPackSerializer.Deserialize<CommonEntityConfigCompatV1>(bytes);
                if (legacy == null)
                {
                    error = "Deserialize<CommonEntityConfigCompatV1> returned null.";
                    return false;
                }

                package = MigrateFromLegacy(entityType, localId, token, legacy);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Current and legacy deserialize both failed. {ex.Message}";
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

        private static IEntityConfigPackage MigrateFromLegacy(EntityType entityType, int localId, string token,
            CommonEntityConfigCompatV1 legacy)
        {
            string resolvedToken = string.IsNullOrEmpty(legacy.EntityIdToken)
                ? (string.IsNullOrWhiteSpace(token) ? localId.ToString() : token)
                : legacy.EntityIdToken;

            switch (entityType)
            {
                case EntityType.TURRET:
                    var turret = new TurretConfigPackage
                    {
                        EntityIdToken = resolvedToken,
                        Base = new TurretBaseData
                        {
                            Name = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty
                        },
                        Ui = new TurretUIData
                        {
                            DisplayName = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty,
                            IconAddress = legacy.IconAddress ?? string.Empty
                        },
                        EntityBlueprintGuid = legacy.EntityBlueprintGuid ?? string.Empty,
                        Version = legacy.Version <= 0 ? 1 : legacy.Version,
                        IsDirty = false
                    };
                    turret.Normalize();
                    return turret;
                case EntityType.ENEMY:
                    var enemy = new EnemyConfigPackage
                    {
                        EntityIdToken = resolvedToken,
                        Base = new EnemyBaseData
                        {
                            Name = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty
                        },
                        Ui = new EnemyUIData
                        {
                            DisplayName = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty,
                            IconAddress = legacy.IconAddress ?? string.Empty
                        },
                        EntityBlueprintGuid = legacy.EntityBlueprintGuid ?? string.Empty,
                        Version = legacy.Version <= 0 ? 1 : legacy.Version,
                        IsDirty = false
                    };
                    enemy.Normalize();
                    return enemy;
                default:
                    var projectile = new ProjectileConfigPackage
                    {
                        EntityIdToken = resolvedToken,
                        Base = new ProjectileBaseData
                        {
                            Name = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty
                        },
                        Ui = new ProjectileUIData
                        {
                            DisplayName = legacy.Name ?? resolvedToken,
                            Description = legacy.Description ?? string.Empty,
                            IconAddress = legacy.IconAddress ?? string.Empty
                        },
                        EntityBlueprintGuid = legacy.EntityBlueprintGuid ?? string.Empty,
                        Version = legacy.Version <= 0 ? 1 : legacy.Version,
                        IsDirty = false
                    };
                    projectile.Normalize();
                    return projectile;
            }
        }
    }
}

