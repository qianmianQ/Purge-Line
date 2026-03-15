#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MemoryPack;
using TowerDefense.Data.Blueprint;
using TowerDefense.Data.EntityData;
using TowerDefense.Editor.Blueprint;
using Unity.Entities;
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

    public class FullValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int MissingAddressablesCount { get; set; }
        public int InvalidBlueprintCount { get; set; }
        public int MissingInIndexCount { get; set; }
        public int BasicErrorCount { get; set; }

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"验证结果: {(IsValid ? "通过" : "失败")}");
            sb.AppendLine($"错误数: {Errors.Count}, 警告数: {Warnings.Count}");

            if (BasicErrorCount > 0)
                sb.AppendLine($"  - 基础错误: {BasicErrorCount}");
            if (MissingAddressablesCount > 0)
                sb.AppendLine($"  - Addressables缺失: {MissingAddressablesCount}");
            if (InvalidBlueprintCount > 0)
                sb.AppendLine($"  - 蓝图无效: {InvalidBlueprintCount}");
            if (MissingInIndexCount > 0)
                sb.AppendLine($"  - 索引缺失: {MissingInIndexCount}");

            return sb.ToString();
        }

        public string GetFullMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetSummary());

            if (Errors.Count > 0)
            {
                sb.AppendLine("\n错误详情:");
                foreach (var error in Errors.Take(50))
                    sb.AppendLine($"  - {error}");
                if (Errors.Count > 50)
                    sb.AppendLine($"  ... (还有 {Errors.Count - 50} 个错误)");
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine("\n警告:");
                foreach (var warning in Warnings.Take(20))
                    sb.AppendLine($"  - {warning}");
                if (Warnings.Count > 20)
                    sb.AppendLine($"  ... (还有 {Warnings.Count - 20} 个警告)");
            }

            return sb.ToString();
        }
    }

    public static class EntityDataEditorUtility
    {
        public const string RegistryAssetPath = "Assets/Data/EntityData/Editor/EntityConfigRegistry.asset";
        public const string SingleEditorAssetDir = "Assets/Data/EntityData/Editor/SingleEditors";
        public const string ConfigBytesRoot = "Assets/Data/EntityData/Configs";
        public const string BlueprintSourceRoot = "Assets/Data/EntityData/Blueprints";
        public const string IndexBytesPath = "Assets/Data/EntityData/entity_index.bytes";
        public const string BlueprintSourceExtension = ".bytes";
        public const string BlueprintCompiledSuffix = ".compiled.bytes";

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
            var fullResult = ValidateAllFull(registry);
            return new EntityValidationResult(fullResult.IsValid, fullResult.GetFullMessage());
        }

        public static FullValidationResult ValidateAllFull(EntityConfigRegistryAsset registry)
        {
            var result = new FullValidationResult();

            // 1. 基础校验 + Addressables + 蓝图
            foreach (var record in registry.MutableRecords)
            {
                // 基础校验
                var itemResult = ValidateRecord(registry, record);
                if (!itemResult.IsValid)
                {
                    result.BasicErrorCount++;
                    result.Errors.Add($"[{record.EntityType}/{record.EntityIdToken}] {itemResult.Message}");
                    continue;
                }

                // Addressables 校验
                if (!ValidateAddressableExists(record.AssetPath, record.Address, out var addrError))
                {
                    result.MissingAddressablesCount++;
                    result.Errors.Add($"[{record.EntityType}/{record.EntityIdToken}] Addressables: {addrError}");
                }

                // 蓝图校验（源蓝图 address + 编译产物 address + hash）
                var package = LoadPackageFromRecord(record);
                if (string.IsNullOrWhiteSpace(package.EntityBlueprintAddress) &&
                    string.IsNullOrWhiteSpace(package.CompiledBlueprintAddress))
                    continue;

                if (!ValidateBlueprintComplete(package.EntityBlueprintAddress, package.CompiledBlueprintAddress, out var bpError))
                {
                    result.InvalidBlueprintCount++;
                    result.Errors.Add($"[{record.EntityType}/{record.EntityIdToken}] 蓝图: {bpError}");
                }
            }

            // 2. 重复 Address 校验
            var duplicateAddressGroups = registry.MutableRecords
                .Where(x => !string.IsNullOrWhiteSpace(x.Address))
                .GroupBy(x => x.Address, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateAddressGroups.Count > 0)
            {
                foreach (var group in duplicateAddressGroups)
                {
                    string members = string.Join(", ",
                        group.Select(x => $"{x.EntityType}/{x.EntityIdToken}"));
                    result.Errors.Add($"重复Address: {group.Key} -> {members}");
                }
            }

            // 3. 索引完整性校验
            if (!ValidateIndexIntegrity(registry, out var missingRecords))
            {
                result.MissingInIndexCount = missingRecords.Count;
                foreach (var record in missingRecords)
                {
                    result.Errors.Add($"[{record.EntityType}/{record.EntityIdToken}] 索引缺失");
                }
            }

            return result;
        }

        public static bool ValidateAddressableExists(string assetPath, string desiredAddress, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "AssetPath 为空";
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                error = $"Asset GUID not found: {assetPath}";
                return false;
            }

            Type settingsDefaultType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsDefaultType == null)
            {
                error = "Unity.Addressables.Editor assembly not found.";
                return false;
            }

            PropertyInfo settingsProp = settingsDefaultType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            object settings = settingsProp?.GetValue(null);
            if (settings == null)
            {
                error = "AddressableAssetSettings not found.";
                return false;
            }

            Type settingsType = settings.GetType();
            MethodInfo findEntryMethod = settingsType.GetMethod("FindAssetEntry", new[] { typeof(string) });
            object entry = findEntryMethod?.Invoke(settings, new object[] { guid });

            if (entry == null)
            {
                error = $"Addressable entry not found for GUID: {guid}";
                return false;
            }

            PropertyInfo addressProp = entry.GetType().GetProperty("address", BindingFlags.Public | BindingFlags.Instance);
            string actualAddress = addressProp?.GetValue(entry) as string;

            if (!string.Equals(actualAddress, desiredAddress, StringComparison.Ordinal))
            {
                error = $"Address 不匹配 (期望: {desiredAddress}, 实际: {actualAddress})";
                return false;
            }

            return true;
        }

        public static bool ValidateBlueprintComplete(string sourceAddress, string compiledAddress, out string error)
        {
            error = string.Empty;
            if (!TryResolveAssetPathByAddress(sourceAddress, out string sourcePath, out string sourceError))
            {
                error = $"源蓝图地址无效: {sourceError}";
                return false;
            }

            if (!sourcePath.EndsWith(BlueprintSourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = $"源蓝图文件后缀必须为 {BlueprintSourceExtension}: {sourcePath}";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath) == null)
            {
                error = $"源蓝图无法通过 AssetDatabase 加载: {sourcePath}";
                return false;
            }

            EntityBlueprintDocument doc;
            byte[] sourceBytes;
            try
            {
                sourceBytes = File.ReadAllBytes(Path.GetFullPath(sourcePath));
                doc = EntityBlueprintBinarySerializer.Load(Path.GetFullPath(sourcePath));
            }
            catch (Exception ex)
            {
                error = $"源蓝图读取失败: {ex.Message}";
                return false;
            }

            if (doc == null)
            {
                error = "源蓝图反序列化失败";
                return false;
            }

            if (!TryResolveAssetPathByAddress(compiledAddress, out string compiledPath, out string compiledError))
            {
                error = $"编译蓝图地址无效: {compiledError}";
                return false;
            }

            if (!compiledPath.EndsWith(BlueprintCompiledSuffix, StringComparison.OrdinalIgnoreCase))
            {
                error = $"编译蓝图文件后缀必须为 {BlueprintCompiledSuffix}: {compiledPath}";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(compiledPath) == null)
            {
                error = $"编译蓝图无法通过 AssetDatabase 加载: {compiledPath}";
                return false;
            }

            CompiledBlueprint compiled;
            try
            {
                var compiledBytes = File.ReadAllBytes(Path.GetFullPath(compiledPath));
                compiled = MemoryPackSerializer.Deserialize<CompiledBlueprint>(compiledBytes);
            }
            catch (Exception ex)
            {
                error = $"编译蓝图读取失败: {ex.Message}";
                return false;
            }

            if (compiled == null)
            {
                error = "编译蓝图反序列化失败";
                return false;
            }

            string expectedHash = ComputeBlueprintHash(sourceBytes);
            if (!string.Equals(compiled.blueprintHash, expectedHash, StringComparison.Ordinal))
            {
                error = "蓝图 hash 不匹配，需要重新编译";
                return false;
            }

            return true;
        }

        public static bool ValidateIndexIntegrity(EntityConfigRegistryAsset registry, out List<EntityConfigRegistryRecord> missingRecords)
        {
            missingRecords = new List<EntityConfigRegistryRecord>();

            string indexAbsolutePath = Path.GetFullPath(IndexBytesPath);
            if (!File.Exists(indexAbsolutePath))
            {
                missingRecords.AddRange(registry.MutableRecords);
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(indexAbsolutePath);
                var index = MemoryPackSerializer.Deserialize<EntityAddressIndex>(bytes);
                if (index == null)
                {
                    missingRecords.AddRange(registry.MutableRecords);
                    return false;
                }

                index.BuildLookupCache();

                foreach (var record in registry.MutableRecords)
                {
                    if (record.LocalId <= 0)
                        continue;

                    if (!index.TryGetAddressFast(record.EntityType, record.LocalId, out _))
                    {
                        missingRecords.Add(record);
                    }
                }
            }
            catch
            {
                missingRecords.AddRange(registry.MutableRecords);
                return false;
            }

            return missingRecords.Count == 0;
        }

        public class RebuildReport
        {
            public int RemovedMissingFiles { get; set; }
            public int ClearedInvalidBlueprints { get; set; }
            public int FixedAddressables { get; set; }
            public List<string> Details { get; } = new List<string>();

            public string GetSummary()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("重建完成:");
                if (RemovedMissingFiles > 0)
                    sb.AppendLine($"  - 删除缺失文件记录: {RemovedMissingFiles}");
                if (ClearedInvalidBlueprints > 0)
                    sb.AppendLine($"  - 清理无效蓝图引用: {ClearedInvalidBlueprints}");
                if (FixedAddressables > 0)
                    sb.AppendLine($"  - 修复 Addressables: {FixedAddressables}");
                if (RemovedMissingFiles == 0 && ClearedInvalidBlueprints == 0 && FixedAddressables == 0)
                    sb.AppendLine("  - 无需清理");
                return sb.ToString();
            }
        }

        public static RebuildReport FullRebuildRegistry(EntityConfigRegistryAsset registry)
        {
            var report = new RebuildReport();

            // 1. 移除文件不存在的记录
            var recordsToRemove = new List<EntityConfigRegistryRecord>();
            foreach (var record in registry.MutableRecords)
            {
                if (string.IsNullOrWhiteSpace(record.AssetPath))
                {
                    recordsToRemove.Add(record);
                    report.Details.Add($"[{record.EntityType}/{record.EntityIdToken}] 移除: AssetPath 为空");
                    continue;
                }

                string absolutePath = Path.GetFullPath(record.AssetPath);
                if (!File.Exists(absolutePath))
                {
                    recordsToRemove.Add(record);
                    report.Details.Add($"[{record.EntityType}/{record.EntityIdToken}] 移除: 文件不存在 {record.AssetPath}");
                }
            }

            foreach (var record in recordsToRemove)
            {
                registry.MutableRecords.Remove(record);
                report.RemovedMissingFiles++;
            }

            // 2. 修复无效的静态编译蓝图（自动重编译）
            foreach (var record in registry.MutableRecords)
            {
                var package = LoadPackageFromRecord(record);
                if (!string.IsNullOrWhiteSpace(package.EntityBlueprintAddress) ||
                    !string.IsNullOrWhiteSpace(package.CompiledBlueprintAddress))
                {
                    if (!ValidateBlueprintComplete(package.EntityBlueprintAddress, package.CompiledBlueprintAddress, out _))
                    {
                        TryCompileBlueprintForRecord(record, package, out _);
                        report.ClearedInvalidBlueprints++;
                        report.Details.Add($"[{record.EntityType}/{record.EntityIdToken}] 重编译静态蓝图");
                    }
                }
            }

            // 3. 修复 Addressables
            foreach (var record in registry.MutableRecords)
            {
                if (!ValidateAddressableExists(record.AssetPath, record.Address, out _))
                {
                    try
                    {
                        EnsureAddressableAddress(record.AssetPath, record.Address);
                        report.FixedAddressables++;
                        report.Details.Add($"[{record.EntityType}/{record.EntityIdToken}] 修复 Addressables");
                    }
                    catch
                    {
                        // 忽略，继续下一个
                    }
                }
            }

            // 4. 重新生成枚举和索引
            SaveRegistry(registry);

            return report;
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

        public sealed class BlueprintBatchCompileReport
        {
            public int Total;
            public int Compiled;
            public int Skipped;
            public int Failed;
            public List<string> Details { get; } = new List<string>();

            public string GetSummary()
            {
                return $"蓝图编译结果: total={Total}, compiled={Compiled}, skipped={Skipped}, failed={Failed}";
            }
        }

        public static BlueprintBatchCompileReport CompileAllBlueprints(EntityConfigRegistryAsset registry)
        {
            return CompileBlueprints(registry, true);
        }

        public static BlueprintBatchCompileReport CompileIncrementalBlueprints(EntityConfigRegistryAsset registry)
        {
            return CompileBlueprints(registry, false);
        }

        public static bool RunBlueprintPreflight(bool forceAll, out string report)
        {
            var registry = GetOrCreateRegistry();
            var compileReport = forceAll ? CompileAllBlueprints(registry) : CompileIncrementalBlueprints(registry);
            var validateResult = ValidateAllFull(registry);
            report = compileReport.GetSummary() + "\n" + validateResult.GetSummary();
            return compileReport.Failed == 0 && validateResult.IsValid;
        }

        private static BlueprintBatchCompileReport CompileBlueprints(EntityConfigRegistryAsset registry, bool forceRecompile)
        {
            var report = new BlueprintBatchCompileReport();

            foreach (var record in registry.MutableRecords)
            {
                report.Total++;
                var package = LoadPackageFromRecord(record);

                if (string.IsNullOrWhiteSpace(package.EntityBlueprintAddress))
                {
                    report.Skipped++;
                    continue;
                }

                if (!forceRecompile &&
                    ValidateBlueprintComplete(package.EntityBlueprintAddress, package.CompiledBlueprintAddress, out _))
                {
                    report.Skipped++;
                    continue;
                }

                if (!TryCompileBlueprintForRecord(record, package, out string detail))
                {
                    report.Failed++;
                    report.Details.Add($"[{record.EntityType}/{record.EntityIdToken}] {detail}");
                    continue;
                }

                report.Compiled++;
            }

            SaveRegistry(registry);
            return report;
        }

        public static bool TryCompileBlueprintForRecord(EntityConfigRegistryRecord record, out string detail)
        {
            var package = LoadPackageFromRecord(record);
            return TryCompileBlueprintForRecord(record, package, out detail);
        }

        public static bool TryCompileBlueprintByToken(string entityIdToken, out string detail)
        {
            var registry = GetOrCreateRegistry();
            var record = registry.MutableRecords.FirstOrDefault(x =>
                string.Equals(x.EntityIdToken, entityIdToken, StringComparison.Ordinal));
            if (record == null)
            {
                detail = $"Record not found: {entityIdToken}";
                return false;
            }

            bool success = TryCompileBlueprintForRecord(record, out detail);
            if (success)
                SaveRegistry(registry);
            return success;
        }

        public static bool TryInstantiateFromCompiledBlueprintByToken(string entityIdToken, out string detail)
        {
            var registry = GetOrCreateRegistry();
            var record = registry.MutableRecords.FirstOrDefault(x =>
                string.Equals(x.EntityIdToken, entityIdToken, StringComparison.Ordinal));
            if (record == null)
            {
                detail = $"Record not found: {entityIdToken}";
                return false;
            }

            var package = LoadPackageFromRecord(record);
            if (string.IsNullOrWhiteSpace(package.CompiledBlueprintAddress))
            {
                detail = "Compiled Blueprint Address 为空，请先点击【编译蓝图】。";
                return false;
            }

            if (!TryResolveAssetPathByAddress(package.CompiledBlueprintAddress, out string compiledAssetPath, out string error))
            {
                detail = $"编译蓝图地址解析失败: {error}";
                return false;
            }

            TextAsset compiledAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(compiledAssetPath);
            if (compiledAsset == null || compiledAsset.bytes == null || compiledAsset.bytes.Length == 0)
            {
                detail = $"编译蓝图资源无效: {compiledAssetPath}";
                return false;
            }

            CompiledBlueprint compiled;
            try
            {
                compiled = MemoryPackSerializer.Deserialize<CompiledBlueprint>(compiledAsset.bytes);
            }
            catch (Exception ex)
            {
                detail = $"编译蓝图反序列化失败: {ex.Message}";
                return false;
            }

            if (compiled == null)
            {
                detail = "编译蓝图反序列化结果为空。";
                return false;
            }

            World world = GetOrCreateEditorWorld(out bool createdNewWorld);
            string blueprintId = string.IsNullOrWhiteSpace(compiled.blueprintId)
                ? entityIdToken
                : compiled.blueprintId;

            var runtimeRegistry = new BlueprintRegistry();
            if (!runtimeRegistry.TryLoad(blueprintId, compiledAsset.bytes, world.EntityManager, out string loadError))
            {
                detail = $"静态实例化失败（加载）: {loadError}";
                return false;
            }

            if (!runtimeRegistry.TryGetPrefab(blueprintId, out Entity prefabEntity))
            {
                detail = "静态实例化失败：无法获取 PrefabEntity。";
                return false;
            }

            Entity instance = world.EntityManager.Instantiate(prefabEntity);
            detail = createdNewWorld
                ? $"静态实例化成功：已创建世界 '{world.Name}'，实体={instance.Index}:{instance.Version}"
                : $"静态实例化成功：世界 '{world.Name}'，实体={instance.Index}:{instance.Version}";
            return true;
        }

        private static World GetOrCreateEditorWorld(out bool createdNewWorld)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                createdNewWorld = false;
                return world;
            }

            world = new World("PurgeLine.EditorBlueprintPreviewWorld", WorldFlags.Game);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            World.DefaultGameObjectInjectionWorld = world;
            createdNewWorld = true;
            return world;
        }

        private static bool TryCompileBlueprintForRecord(EntityConfigRegistryRecord record, IEntityConfigPackage package,
            out string detail)
        {
            detail = string.Empty;
            if (string.IsNullOrWhiteSpace(package.EntityBlueprintAddress))
            {
                detail = "源蓝图 Address 为空";
                return false;
            }

            if (!TryResolveAssetPathByAddress(package.EntityBlueprintAddress, out string sourceAssetPath, out string error))
            {
                detail = $"源蓝图不存在: {error}";
                return false;
            }

            try
            {
                string sourceAbsolutePath = Path.GetFullPath(sourceAssetPath);
                byte[] sourceBytes = File.ReadAllBytes(sourceAbsolutePath);
                string blueprintHash = ComputeBlueprintHash(sourceBytes);

                var doc = EntityBlueprintBinarySerializer.Load(sourceAbsolutePath);
                if (doc == null)
                {
                    detail = "源蓝图反序列化为空";
                    return false;
                }

                byte[] compiledBytes = BlueprintCompiler.CompileToBytes(doc, blueprintHash);

                string compiledAssetPath = BuildCompiledBlueprintAssetPath(sourceAssetPath);
                EnsureAssetDirectory(compiledAssetPath);
                File.WriteAllBytes(Path.GetFullPath(compiledAssetPath), compiledBytes);
                AssetDatabase.ImportAsset(compiledAssetPath, ImportAssetOptions.ForceSynchronousImport);

                string sourceSlug = BuildBlueprintSlugFromAssetPath(sourceAssetPath);
                string compiledAddress = EntityDataAddressRules.BuildCompiledBlueprintAddress(sourceSlug);
                package.CompiledBlueprintAddress = EnsureAddressableAddress(compiledAssetPath, compiledAddress);
                SavePackageToFile(package, record.AssetPath);

                detail = $"编译成功: {compiledAssetPath}";
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return false;
            }
        }

        public static string BuildCompiledBlueprintAssetPath(string sourceAssetPath)
        {
            string normalized = sourceAssetPath.Replace('\\', '/');
            if (normalized.EndsWith(BlueprintSourceExtension, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - BlueprintSourceExtension.Length);
            return normalized + BlueprintCompiledSuffix;
        }

        public static string BuildBlueprintSlugFromAssetPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            string rootPrefix = BlueprintSourceRoot + "/";
            if (normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(rootPrefix.Length);

            if (normalized.EndsWith(BlueprintSourceExtension, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - BlueprintSourceExtension.Length);

            return EntityDataAddressRules.ToSlug(normalized.Replace('/', '_'));
        }

        public static string ComputeBlueprintHash(byte[] sourceBytes)
        {
            if (sourceBytes == null)
                return string.Empty;

            byte[] versionBytes = Encoding.UTF8.GetBytes($"|compiler:{CompiledBlueprint.CurrentVersion}");
            byte[] merged = new byte[sourceBytes.Length + versionBytes.Length];
            Buffer.BlockCopy(sourceBytes, 0, merged, 0, sourceBytes.Length);
            Buffer.BlockCopy(versionBytes, 0, merged, sourceBytes.Length, versionBytes.Length);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(merged);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        public static bool TryResolveAssetPathByAddress(string address, out string assetPath, out string error)
        {
            assetPath = string.Empty;
            error = string.Empty;
            string matchedGuid = string.Empty;
            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Address 为空";
                return false;
            }

            if (!TryGetAddressableSettings(out object settings, out Type settingsType, out error))
                return false;

            PropertyInfo groupsProp = settingsType.GetProperty("groups", BindingFlags.Public | BindingFlags.Instance);
            if (groupsProp?.GetValue(settings) is not System.Collections.IEnumerable groups)
            {
                error = "Addressables groups 获取失败";
                return false;
            }

            foreach (object group in groups)
            {
                if (group == null)
                    continue;

                PropertyInfo entriesProp = group.GetType().GetProperty("entries", BindingFlags.Public | BindingFlags.Instance);
                if (entriesProp?.GetValue(group) is not System.Collections.IEnumerable entries)
                    continue;

                foreach (object entry in entries)
                {
                    if (entry == null)
                        continue;

                    PropertyInfo addressProp = entry.GetType().GetProperty("address", BindingFlags.Public | BindingFlags.Instance);
                    string currentAddress = addressProp?.GetValue(entry) as string;
                    if (!string.Equals(currentAddress, address, StringComparison.Ordinal))
                        continue;

                    PropertyInfo guidProp = entry.GetType().GetProperty("guid", BindingFlags.Public | BindingFlags.Instance);
                    string guid = guidProp?.GetValue(entry) as string;
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        error = $"Address '{address}' 对应 GUID 为空";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(matchedGuid))
                    {
                        matchedGuid = guid;
                        continue;
                    }

                    if (!string.Equals(matchedGuid, guid, StringComparison.Ordinal))
                    {
                        error = $"Address '{address}' 重复映射到多个资源 GUID";
                        return false;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(matchedGuid))
            {
                error = $"Address '{address}' 未找到";
                return false;
            }

            assetPath = AssetDatabase.GUIDToAssetPath(matchedGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = $"Address '{address}' 对应 GUID 无效";
                return false;
            }

            return true;
        }

        private static bool TryGetAddressableSettings(out object settings, out Type settingsType, out string error)
        {
            settings = null;
            settingsType = null;
            error = string.Empty;

            Type settingsDefaultType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsDefaultType == null)
            {
                error = "Unity.Addressables.Editor assembly not found.";
                return false;
            }

            PropertyInfo settingsProp = settingsDefaultType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            settings = settingsProp?.GetValue(null);
            if (settings == null)
            {
                error = "AddressableAssetSettings not found.";
                return false;
            }

            settingsType = settings.GetType();
            return true;
        }

        public static bool TryPickBlueprintAddress(out string address)
        {
            string absolutePath = EditorUtility.OpenFilePanel("Select Entity Blueprint", Application.dataPath, "bytes");
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                address = string.Empty;
                return false;
            }

            if (!TryConvertAbsolutePathToAssetPath(absolutePath, out string assetPath))
            {
                address = string.Empty;
                return false;
            }

            string slug = BuildBlueprintSlugFromAssetPath(assetPath);
            string desiredAddress = EntityDataAddressRules.BuildBlueprintSourceAddress(slug);
            address = EnsureAddressableAddress(assetPath, desiredAddress);
            return !string.IsNullOrWhiteSpace(address);
        }

        public static string CreateAndOpenBlueprint(string entityIdToken)
        {
            string slug = $"{EntityDataAddressRules.ToSlug(entityIdToken)}_{DateTime.Now:yyyyMMddHHmmss}";
            string fileName = $"{slug}{BlueprintSourceExtension}";
            string targetAssetPath = $"{BlueprintSourceRoot}/{fileName}";
            EnsureAssetDirectory(targetAssetPath);

            string absolutePath = Path.GetFullPath(targetAssetPath);
            var document = new EntityBlueprintDocument { blueprintName = entityIdToken };
            EntityBlueprintBinarySerializer.Save(absolutePath, document);
            AssetDatabase.Refresh();

            string sourceAddress = EnsureAddressableAddress(targetAssetPath, EntityDataAddressRules.BuildBlueprintSourceAddress(slug));
            EntityBlueprintEditorWindow.OpenAndLoad(absolutePath);
            return sourceAddress;
        }

        public static bool IsBlueprintAddressValid(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            if (!TryResolveAssetPathByAddress(address, out string assetPath, out _))
                return false;

            string absolutePath = Path.GetFullPath(assetPath);
            return File.Exists(absolutePath);
        }

        public static bool OpenBlueprintByAddress(string address)
        {
            if (!TryResolveAssetPathByAddress(address, out string assetPath, out _)) return false;
            string absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath)) return false;
            EntityBlueprintEditorWindow.OpenAndLoad(absolutePath);
            return true;
        }

        public static string GetBlueprintSummary(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return "未关联蓝图";
            if (!TryResolveAssetPathByAddress(address, out string path, out string error))
                return $"蓝图 Address: {address} (路径无效: {error})";
            return $"蓝图 Address: {address}\n路径: {path}";
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

