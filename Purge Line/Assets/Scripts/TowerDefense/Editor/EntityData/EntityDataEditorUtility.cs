#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MemoryPack;
using TowerDefense.Data.Blueprint;
using TowerDefense.Data.EntityData;
using TowerDefense.Editor.Blueprint;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    public readonly struct EntityValidationResult
    {
        public EntityValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public bool IsValid { get; }
        public string Message { get; }
    }

    public static class EntityDataEditorUtility
    {
        public const string RegistryAssetPath = "Assets/Data/EntityData/Editor/EntityConfigRegistry.asset";
        public const string SingleEditorAssetDir = "Assets/Data/EntityData/Editor/SingleEditors";
        public const string ConfigBytesRoot = "Assets/Data/EntityData/Configs";
        public const string IndexBytesPath = "Assets/Data/EntityData/entity_index.bytes";

        public static EntityConfigRegistryAsset GetOrCreateRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<EntityConfigRegistryAsset>(RegistryAssetPath);
            if (registry != null)
                return registry;

            EnsureAssetDirectory(RegistryAssetPath);
            registry = ScriptableObject.CreateInstance<EntityConfigRegistryAsset>();
            AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            AssetDatabase.SaveAssets();
            return registry;
        }

        public static ScriptableObject GetOrCreateTypedEditorAsset(EntityType entityType)
        {
            if (!AssetDatabase.IsValidFolder(SingleEditorAssetDir))
            {
                Directory.CreateDirectory(Path.GetFullPath(SingleEditorAssetDir));
                AssetDatabase.Refresh();
            }

            string path = $"{SingleEditorAssetDir}/{entityType}_SingleEditor.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
                return existing;

            ScriptableObject created = entityType switch
            {
                EntityType.TURRET => ScriptableObject.CreateInstance<TurretConfigEditorAsset>(),
                EntityType.ENEMY => ScriptableObject.CreateInstance<EnemyConfigEditorAsset>(),
                EntityType.PROJECTILE => ScriptableObject.CreateInstance<ProjectileConfigEditorAsset>(),
                _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null)
            };

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static EntityConfigRegistryRecord CreateNewRecord(EntityConfigRegistryAsset registry, EntityType entityType,
            string displayName)
        {
            if (!TryValidateCreateName(registry, entityType, displayName, out var validateError))
                throw new InvalidOperationException(validateError);

            string sourceName = displayName.Trim();
            string enumSuffix = EntityIdEnumGenerator.Sanitize(sourceName);
            string uniqueToken = $"{entityType}_{enumSuffix}";
            string typeDir = EntityDataAddressRules.ToSlug(entityType.ToString());
            string slug = EntityDataAddressRules.ToSlug(sourceName);
            string assetPath = $"{ConfigBytesRoot}/{typeDir}/{slug}.bytes";
            string address = EntityDataAddressRules.BuildEntityConfigAddress(entityType, slug);

            switch (entityType)
            {
                case EntityType.TURRET:
                    SavePackageToFile(new TurretConfigPackage
                    {
                        EntityIdToken = uniqueToken,
                        Base = new TurretBaseData { Name = sourceName },
                        Ui = new TurretUIData { DisplayName = sourceName }
                    }, assetPath);
                    break;
                case EntityType.ENEMY:
                    SavePackageToFile(new EnemyConfigPackage
                    {
                        EntityIdToken = uniqueToken,
                        Base = new EnemyBaseData { Name = sourceName },
                        Ui = new EnemyUIData { DisplayName = sourceName }
                    }, assetPath);
                    break;
                case EntityType.PROJECTILE:
                    SavePackageToFile(new ProjectileConfigPackage
                    {
                        EntityIdToken = uniqueToken,
                        Base = new ProjectileBaseData { Name = sourceName },
                        Ui = new ProjectileUIData { DisplayName = sourceName }
                    }, assetPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null);
            }

            EnsureAddressableAddress(assetPath, address);

            var record = new EntityConfigRegistryRecord
            {
                EntityType = entityType,
                DisplayName = sourceName,
                EntityIdToken = uniqueToken,
                EntityIdEnumName = enumSuffix,
                Address = address,
                AssetPath = assetPath
            };
            registry.MutableRecords.Add(record);
            SaveRegistry(registry);
            return record;
        }

        public static bool TryDeleteRecordWithReferenceCheck(EntityConfigRegistryAsset registry,
            EntityConfigRegistryRecord record, out string report)
        {
            List<string> refs = ScanAddressReferences(registry, record.Address, record.EntityIdToken);
            if (refs.Count > 0)
            {
                report = "发现引用，禁止删除:\n" + string.Join("\n", refs);
                return false;
            }

            registry.MutableRecords.Remove(record);
            if (!string.IsNullOrWhiteSpace(record.AssetPath))
                AssetDatabase.DeleteAsset(record.AssetPath);

            SaveRegistry(registry);
            report = "删除成功";
            return true;
        }

        public static IEntityConfigPackage LoadPackageFromRecord(EntityConfigRegistryRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.AssetPath))
                return BuildTypeFallback(record?.EntityType ?? EntityType.TURRET, record?.EntityIdToken ?? "UNKNOWN", "Record invalid.");

            string absolutePath = Path.GetFullPath(record.AssetPath);
            if (!File.Exists(absolutePath))
                return BuildTypeFallback(record.EntityType, record.EntityIdToken, "Data file not found.");

            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (!EntityConfigCompatibility.TryDeserialize(bytes, record.EntityType, record.LocalId, record.EntityIdToken,
                    out var package, out var error))
            {
                return BuildTypeFallback(record.EntityType, record.EntityIdToken, error);
            }

            package.EntityIdToken = record.EntityIdToken;
            package.Normalize();
            return package;
        }

        public static void SaveTypedEditor(EntityConfigRegistryAsset registry, ScriptableObject editorAsset)
        {
            switch (editorAsset)
            {
                case TurretConfigEditorAsset turretEditor:
                    SaveTypedEditorCore(registry, turretEditor, turretEditor.CurrentConfig);
                    break;
                case EnemyConfigEditorAsset enemyEditor:
                    SaveTypedEditorCore(registry, enemyEditor, enemyEditor.CurrentConfig);
                    break;
                case ProjectileConfigEditorAsset projectileEditor:
                    SaveTypedEditorCore(registry, projectileEditor, projectileEditor.CurrentConfig);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported typed editor asset: {editorAsset.GetType().Name}");
            }

            EditorUtility.SetDirty(editorAsset);
            AssetDatabase.SaveAssets();
        }

        public static void LoadIntoTypedEditor(EntityConfigRegistryRecord record, ScriptableObject editorAsset)
        {
            var package = LoadPackageFromRecord(record);
            switch (editorAsset)
            {
                case TurretConfigEditorAsset turretEditor:
                    turretEditor.LoadFrom(record.EntityType, record.LocalId, record.EntityIdToken, record.Address,
                        record.AssetPath, package as TurretConfigPackage ?? TurretConfigPackage.BuildFallback(record.EntityIdToken, "Type mismatch"));
                    break;
                case EnemyConfigEditorAsset enemyEditor:
                    enemyEditor.LoadFrom(record.EntityType, record.LocalId, record.EntityIdToken, record.Address,
                        record.AssetPath, package as EnemyConfigPackage ?? EnemyConfigPackage.BuildFallback(record.EntityIdToken, "Type mismatch"));
                    break;
                case ProjectileConfigEditorAsset projectileEditor:
                    projectileEditor.LoadFrom(record.EntityType, record.LocalId, record.EntityIdToken, record.Address,
                        record.AssetPath, package as ProjectileConfigPackage ?? ProjectileConfigPackage.BuildFallback(record.EntityIdToken, "Type mismatch"));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported typed editor asset: {editorAsset.GetType().Name}");
            }

            EditorUtility.SetDirty(editorAsset);
        }

        public static EntityValidationResult ValidateRecord(EntityConfigRegistryAsset registry, EntityConfigRegistryRecord record)
        {
            if (record.LocalId <= 0)
                return new EntityValidationResult(false, "LocalId 未生成或非法");
            if (string.IsNullOrWhiteSpace(record.Address))
                return new EntityValidationResult(false, "Address 为空");
            if (string.IsNullOrWhiteSpace(record.AssetPath) || !File.Exists(Path.GetFullPath(record.AssetPath)))
                return new EntityValidationResult(false, "bytes 文件不存在");

            try
            {
                EnsureAddressableAddress(record.AssetPath, record.Address);
            }
            catch (Exception ex)
            {
                return new EntityValidationResult(false, $"Addressables校验失败: {ex.Message}");
            }

            var package = LoadPackageFromRecord(record);
            if (string.IsNullOrWhiteSpace(package.DisplayNameForLog))
                return new EntityValidationResult(false, "UI/Base 名称为空");

            bool hasDup = registry.MutableRecords.Count(x => string.Equals(x.Address, record.Address, StringComparison.Ordinal)) > 1;
            if (hasDup)
                return new EntityValidationResult(false, "存在重复 address");

            return new EntityValidationResult(true, "校验通过");
        }

        public static EntityValidationResult ValidateAll(EntityConfigRegistryAsset registry)
        {
            var messages = new List<string>();
            bool success = true;

            foreach (var record in registry.MutableRecords)
            {
                var itemResult = ValidateRecord(registry, record);
                if (!itemResult.IsValid)
                {
                    success = false;
                    messages.Add($"[{record.EntityType}/{record.EntityIdToken}] {itemResult.Message}");
                }
            }

            var duplicateAddressGroups = registry.MutableRecords
                .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                .GroupBy(x => x.Address, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateAddressGroups.Count > 0)
            {
                success = false;
                foreach (var group in duplicateAddressGroups)
                {
                    string members = string.Join(", ",
                        group.Select(x => $"{x.EntityType}/{x.EntityIdToken}"));
                    messages.Add($"重复Address: {group.Key} -> {members}");
                }
            }

            return success
                ? new EntityValidationResult(true, "全量校验通过")
                : new EntityValidationResult(false, string.Join("\n", messages));
        }

        public static void RefreshGeneratedArtifacts(EntityConfigRegistryAsset registry)
        {
            EntityIdEnumGenerator.Generate(registry);
            BuildRuntimeIndexBytes(registry);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static string EnsureAddressableAddress(string assetPath, string desiredAddress)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException($"Asset GUID not found: {assetPath}");

            Type settingsDefaultType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsDefaultType == null)
                throw new InvalidOperationException("Unity.Addressables.Editor assembly not found.");

            PropertyInfo settingsProp = settingsDefaultType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            object settings = settingsProp?.GetValue(null);
            if (settings == null)
                throw new InvalidOperationException("AddressableAssetSettings not found.");

            Type settingsType = settings.GetType();
            MethodInfo findEntryMethod = settingsType.GetMethod("FindAssetEntry", new[] { typeof(string) });
            object entry = findEntryMethod?.Invoke(settings, new object[] { guid });

            if (entry == null)
            {
                PropertyInfo defaultGroupProp = settingsType.GetProperty("DefaultGroup", BindingFlags.Public | BindingFlags.Instance);
                object defaultGroup = defaultGroupProp?.GetValue(settings);
                if (defaultGroup == null)
                    throw new InvalidOperationException("Addressables default group is null.");

                MethodInfo createOrMoveEntryMethod = settingsType.GetMethod("CreateOrMoveEntry",
                    new[] { typeof(string), defaultGroup.GetType(), typeof(bool), typeof(bool) });
                if (createOrMoveEntryMethod == null)
                    throw new InvalidOperationException("Addressables CreateOrMoveEntry API not found.");

                entry = createOrMoveEntryMethod.Invoke(settings, new object[] { guid, defaultGroup, false, false });
            }

            PropertyInfo addressProp = entry.GetType().GetProperty("address", BindingFlags.Public | BindingFlags.Instance);
            addressProp?.SetValue(entry, desiredAddress);
            EditorUtility.SetDirty((UnityEngine.Object)settings);
            return desiredAddress;
        }

        public static string EnsureAddressableAddressForSprite(Sprite sprite)
        {
            if (sprite == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return EnsureAddressableAddress(path, EntityDataAddressRules.BuildDefaultSpriteAddress(sprite.name));
        }

        public static string EnsureAddressableAddressForAsset(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return EnsureAddressableAddress(path, $"td/asset/{EntityDataAddressRules.ToSlug(asset.name)}");
        }

        public static bool TryPickBlueprintGuid(out string guid)
        {
            string absolutePath = EditorUtility.OpenFilePanel("Select Entity Blueprint", Application.dataPath, "entitybp");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                guid = string.Empty;
                return false;
            }

            if (!TryConvertAbsolutePathToAssetPath(absolutePath, out string assetPath))
            {
                guid = string.Empty;
                return false;
            }

            guid = AssetDatabase.AssetPathToGUID(assetPath);
            return !string.IsNullOrWhiteSpace(guid);
        }

        public static string CreateAndOpenBlueprint(string entityIdToken)
        {
            string fileName = $"{EntityDataAddressRules.ToSlug(entityIdToken)}_{DateTime.Now:yyyyMMddHHmmss}.entitybp";
            string targetAssetPath = $"Assets/Data/EntityData/Blueprints/{fileName}";
            EnsureAssetDirectory(targetAssetPath);

            string absolutePath = Path.GetFullPath(targetAssetPath);
            var document = new EntityBlueprintDocument { blueprintName = entityIdToken };
            EntityBlueprintBinarySerializer.Save(absolutePath, document);
            AssetDatabase.Refresh();

            string guid = AssetDatabase.AssetPathToGUID(targetAssetPath);
            EntityBlueprintEditorWindow.OpenAndLoad(absolutePath);
            return guid;
        }

        public static bool IsBlueprintGuidValid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return false;

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string absolutePath = Path.GetFullPath(assetPath);
            return File.Exists(absolutePath);
        }

        public static bool OpenBlueprintByGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return false;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            string absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath)) return false;
            EntityBlueprintEditorWindow.OpenAndLoad(absolutePath);
            return true;
        }

        public static string GetBlueprintSummary(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return "未关联蓝图";
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                return $"蓝图 GUID: {guid} (路径无效)";
            return $"蓝图 GUID: {guid}\n路径: {path}";
        }

        private static void SaveTypedEditorCore(EntityConfigRegistryAsset registry, ScriptableObject editorAsset, IEntityConfigPackage package)
        {
            var record = registry.MutableRecords.FirstOrDefault(x => string.Equals(x.EntityIdToken, package.EntityIdToken, StringComparison.Ordinal));
            if (record == null)
                throw new InvalidOperationException($"Record not found for token '{package.EntityIdToken}'");

            package.Normalize();
            SavePackageToFile(package, record.AssetPath);
            record.DisplayName = package.DisplayNameForLog;
            string slug = EntityDataAddressRules.ToSlug(record.DisplayName);
            record.Address = EntityDataAddressRules.BuildEntityConfigAddress(record.EntityType, slug);
            EnsureAddressableAddress(record.AssetPath, record.Address);
            SaveRegistry(registry);
            EditorUtility.SetDirty(editorAsset);
        }

        private static void SaveRegistry(EntityConfigRegistryAsset registry)
        {
            EditorUtility.SetDirty(registry);
            RefreshGeneratedArtifacts(registry);
        }

        private static void BuildRuntimeIndexBytes(EntityConfigRegistryAsset registry)
        {
            var index = new EntityAddressIndex();
            foreach (EntityType entityType in Enum.GetValues(typeof(EntityType)))
            {
                if (entityType == EntityType.Max)
                    continue;

                var bucket = new EntityTypeAddressBucket { EntityType = entityType };
                foreach (var record in registry.MutableRecords.Where(x => x.EntityType == entityType).OrderBy(x => x.LocalId))
                {
                    bucket.Items.Add(new EntityAddressItem
                    {
                        LocalId = record.LocalId,
                        EntityIdToken = record.EntityIdToken,
                        EnumName = record.EntityIdEnumName,
                        Address = record.Address
                    });
                }

                index.TypeBuckets.Add(bucket);
            }

            byte[] bytes = MemoryPackSerializer.Serialize(index);
            EnsureAssetDirectory(IndexBytesPath);
            File.WriteAllBytes(Path.GetFullPath(IndexBytesPath), bytes);
            AssetDatabase.ImportAsset(IndexBytesPath);
            EnsureAddressableAddress(IndexBytesPath, EntityDataAddressRules.IndexAddress);
        }

        private static void SavePackageToFile(IEntityConfigPackage package, string assetPath)
        {
            byte[] data = package switch
            {
                TurretConfigPackage turret => MemoryPackSerializer.Serialize(turret),
                EnemyConfigPackage enemy => MemoryPackSerializer.Serialize(enemy),
                ProjectileConfigPackage projectile => MemoryPackSerializer.Serialize(projectile),
                _ => throw new InvalidOperationException($"Unsupported package type: {package.GetType().Name}")
            };

            EnsureAssetDirectory(assetPath);
            File.WriteAllBytes(Path.GetFullPath(assetPath), data);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static IEntityConfigPackage BuildTypeFallback(EntityType entityType, string token, string reason)
        {
            return entityType switch
            {
                EntityType.TURRET => TurretConfigPackage.BuildFallback(token, reason),
                EntityType.ENEMY => EnemyConfigPackage.BuildFallback(token, reason),
                EntityType.PROJECTILE => ProjectileConfigPackage.BuildFallback(token, reason),
                _ => TurretConfigPackage.BuildFallback(token, reason)
            };
        }

        private static List<string> ScanAddressReferences(EntityConfigRegistryAsset registry, string targetAddress, string deletingToken)
        {
            var refs = new List<string>();
            foreach (var candidate in registry.MutableRecords)
            {
                if (candidate.EntityIdToken == deletingToken)
                    continue;

                var package = LoadPackageFromRecord(candidate);
                switch (package)
                {
                    case TurretConfigPackage turret:
                        CollectRefs(candidate, turret.Ui.IconAddress, targetAddress, refs);
                        CollectRefs(candidate, turret.Ui.PreviewAddress, targetAddress, refs);
                        CollectRefs(candidate, turret.ExtraSfxAddress, targetAddress, refs);
                        break;
                    case EnemyConfigPackage enemy:
                        CollectRefs(candidate, enemy.Ui.IconAddress, targetAddress, refs);
                        CollectRefs(candidate, enemy.Ui.PreviewAddress, targetAddress, refs);
                        CollectRefs(candidate, enemy.ExtraSfxAddress, targetAddress, refs);
                        break;
                    case ProjectileConfigPackage projectile:
                        CollectRefs(candidate, projectile.Ui.IconAddress, targetAddress, refs);
                        CollectRefs(candidate, projectile.Ui.PreviewAddress, targetAddress, refs);
                        CollectRefs(candidate, projectile.ExtraSfxAddress, targetAddress, refs);
                        break;
                }
            }

            return refs;
        }

        private static void CollectRefs(EntityConfigRegistryRecord owner, string value, string targetAddress, ICollection<string> refs)
        {
            if (string.Equals(value, targetAddress, StringComparison.Ordinal))
                refs.Add($"[{owner.EntityType}/{owner.EntityIdToken}] 引用Address: {targetAddress}");
        }

        public static bool TryValidateCreateName(EntityConfigRegistryAsset registry, EntityType entityType,
            string rawName, out string message)
        {
            if (registry == null)
            {
                message = "Registry 不存在";
                return false;
            }

            string name = rawName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "名称不能为空";
                return false;
            }

            string enumName = EntityIdEnumGenerator.Sanitize(name);
            if (string.Equals(enumName, "UNKNOWN", StringComparison.Ordinal))
            {
                message = "名称无效，无法生成枚举名";
                return false;
            }

            string slug = EntityDataAddressRules.ToSlug(name);
            string address = EntityDataAddressRules.BuildEntityConfigAddress(entityType, slug);

            bool duplicateName = registry.MutableRecords.Any(x => x.EntityType == entityType &&
                string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            if (duplicateName)
            {
                message = "同分类下名称重复";
                return false;
            }

            bool duplicateEnumName = registry.MutableRecords.Any(x => x.EntityType == entityType &&
                string.Equals(x.EntityIdEnumName, enumName, StringComparison.Ordinal));
            if (duplicateEnumName)
            {
                message = "同分类下枚举名重复";
                return false;
            }

            bool duplicateAddress = registry.MutableRecords.Any(x => x.EntityType == entityType &&
                string.Equals(x.Address, address, StringComparison.Ordinal));
            if (duplicateAddress)
            {
                message = "同分类下 address 后缀重复";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        // Keep names/addresses strict and deterministic; no auto-suffix fallback in this phase.

        private static bool TryConvertAbsolutePathToAssetPath(string absolutePath, out string assetPath)
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string normalized = absolutePath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                assetPath = string.Empty;
                return false;
            }

            assetPath = "Assets" + normalized.Substring(dataPath.Length);
            return true;
        }
    }
}
#endif

