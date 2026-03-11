using System;
using System.IO;
using MemoryPack;
using Microsoft.Extensions.Logging;
using MELogger = Microsoft.Extensions.Logging.ILogger;
using UnityEngine;

namespace TowerDefense.Data
{
    /// <summary>
    /// 关卡配置加载器
    ///
    /// 提供同步和异步两种加载路径：
    /// - 同步：用于编辑器工具和测试
    /// - 异步：用于运行时关卡加载（UniTask）
    ///
    /// 加载优先级：
    /// 1. persistentDataPath（热更新覆盖）
    /// 2. Resources（内置资源）
    /// 3. 直接文件路径（编辑器调试）
    /// </summary>
    public static class LevelConfigLoader
    {
        private static readonly MELogger _logger = GameLogger.Create("LevelConfigLoader");

        /// <summary>关卡文件存储的 Resources 子目录</summary>
        public const string ResourcesSubDir = "Levels";

        /// <summary>关卡文件存储的 Assets 目录</summary>
        public const string AssetsDir = "Assets/Data/Levels";

        /// <summary>热更新目录名</summary>
        public const string HotUpdateDir = "Levels";

        // ── 同步加载 ─────────────────────────────────────────

        /// <summary>
        /// 从 Resources 同步加载关卡配置
        /// </summary>
        /// <param name="levelId">关卡ID（不含扩展名）</param>
        /// <returns>关卡配置，加载失败返回 null</returns>
        public static LevelConfig LoadFromResources(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
            {
                _logger.LogError("[LevelConfigLoader] levelId is null or empty");
                return null;
            }

            // 优先检查热更新目录
            var hotUpdatePath = GetHotUpdatePath(levelId);
            if (File.Exists(hotUpdatePath))
            {
                _logger.LogInformation("[LevelConfigLoader] Loading from hot-update: {0}", hotUpdatePath);
                return LoadFromFile(hotUpdatePath);
            }

            // 从 Resources 加载
            string resourcePath = $"{ResourcesSubDir}/{levelId}";
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                _logger.LogError("[LevelConfigLoader] Resource not found: {0}", resourcePath);
                return null;
            }

            try
            {
                var config = MemoryPackSerializer.Deserialize<LevelConfig>(textAsset.bytes);
                if (config == null)
                {
                    _logger.LogError("[LevelConfigLoader] Deserialization returned null for: {0}", levelId);
                    return null;
                }

                if (!config.Validate(out var error))
                {
                    _logger.LogError("[LevelConfigLoader] Validation failed for {0}: {1}", levelId, error);
                    return null;
                }

                _logger.LogInformation("[LevelConfigLoader] Loaded from Resources: {0} ({1}x{2})",
                    levelId, config.Width, config.Height);
                return config;
            }
            catch (Exception e)
            {
                _logger.LogError("[LevelConfigLoader] Failed to deserialize {0}: {1}", levelId, e.Message);
                return null;
            }
            finally
            {
                Resources.UnloadAsset(textAsset);
            }
        }

        /// <summary>
        /// 从文件路径同步加载关卡配置
        /// </summary>
        /// <param name="filePath">完整文件路径</param>
        /// <returns>关卡配置，加载失败返回 null</returns>
        public static LevelConfig LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("[LevelConfigLoader] File not found: {0}", filePath);
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                var config = MemoryPackSerializer.Deserialize<LevelConfig>(bytes);

                if (config == null)
                {
                    _logger.LogError("[LevelConfigLoader] Deserialization returned null for: {0}", filePath);
                    return null;
                }

                if (!config.Validate(out var error))
                {
                    _logger.LogError("[LevelConfigLoader] Validation failed for {0}: {1}", filePath, error);
                    return null;
                }

                _logger.LogInformation("[LevelConfigLoader] Loaded from file: {0} ({1}x{2})",
                    filePath, config.Width, config.Height);
                return config;
            }
            catch (Exception e)
            {
                _logger.LogError("[LevelConfigLoader] Failed to load {0}: {1}", filePath, e.Message);
                return null;
            }
        }

        /// <summary>
        /// 从 byte[] 直接反序列化（用于编辑器和测试）
        /// </summary>
        public static LevelConfig LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                _logger.LogError("[LevelConfigLoader] Data is null or empty");
                return null;
            }

            try
            {
                var config = MemoryPackSerializer.Deserialize<LevelConfig>(data);
                if (config != null && config.Validate(out _))
                {
                    return config;
                }

                _logger.LogError("[LevelConfigLoader] Deserialization or validation failed");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError("[LevelConfigLoader] Failed to deserialize bytes: {0}", e.Message);
                return null;
            }
        }

        // ── 序列化 / 保存 ─────────────────────────────────────

        /// <summary>
        /// 将关卡配置序列化为 byte[]
        /// </summary>
        public static byte[] Serialize(LevelConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!config.Validate(out var error))
                throw new ArgumentException($"Invalid config: {error}");

            return MemoryPackSerializer.Serialize(config);
        }

        /// <summary>
        /// 将关卡配置保存到文件
        /// </summary>
        /// <param name="config">关卡配置</param>
        /// <param name="filePath">目标文件路径</param>
        public static void SaveToFile(LevelConfig config, string filePath)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            byte[] data = Serialize(config);

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, data);
            _logger.LogInformation("[LevelConfigLoader] Saved to: {0} ({1} bytes)", filePath, data.Length);
        }

        // ── 路径工具 ─────────────────────────────────────────

        /// <summary>获取热更新文件路径</summary>
        public static string GetHotUpdatePath(string levelId)
        {
            return Path.Combine(Application.persistentDataPath, HotUpdateDir, $"{levelId}.bytes");
        }

        /// <summary>获取 Assets 下的编辑器文件路径</summary>
        public static string GetEditorFilePath(string levelId)
        {
            return Path.Combine(AssetsDir, $"{levelId}.bytes");
        }

        /// <summary>获取 Resources 下的文件路径（用于编辑器导出）</summary>
        public static string GetResourcesFilePath(string levelId)
        {
            return Path.Combine("Assets/Resources", ResourcesSubDir, $"{levelId}.bytes");
        }
    }
}

