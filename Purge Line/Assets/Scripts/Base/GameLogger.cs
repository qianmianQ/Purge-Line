using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;
using ZLogger.Unity;

/// <summary>
/// 全局日志工具类
///
/// 生命周期：
///   Uninitialized → Initializing → Ready → Disposed
///
/// 外部用法：
///   显式初始化（推荐）：GameLogger.Init() 或 GameLogger.Init(builder => ...)
///   懒加载（按需）：    直接调用 GameLogger.Create&lt;T&gt;()，首次使用自动用默认配置初始化
///   销毁：             GameLogger.Dispose()（GameFramework.OnDestroy 调用）
/// </summary>
public static class GameLogger
{
    // ── 生命周期状态 ───────────────────────────────────────────

    private enum State { Uninitialized = 0, Initializing, Ready, Disposed }

    // volatile 保证多线程可见性
    private static volatile State _state = State.Uninitialized;
    private static readonly object _lock = new object();

    // ── 内部成员 ───────────────────────────────────────────────

    private static ILoggerFactory _factory;

    // 缓存已创建的 Logger，避免重复分配（key = category name）
    private static readonly ConcurrentDictionary<string, ILogger> _cache =
        new ConcurrentDictionary<string, ILogger>(StringComparer.Ordinal);

    // ── 公开状态属性 ────────────────────────────────────────────

    public static bool IsInitialized => _state == State.Ready;
    public static bool IsDisposed    => _state == State.Disposed;

    // ── 快捷调试方法 ────────────────────────────────────────────

    /// <summary>
    /// 直接打印 Trace 级别日志（全局分类）。
    /// </summary>
    public static void LogTrace(string message, params object[] args)
    {
        if(!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger("Global").LogTrace(message, args);
    }

    /// <summary>
    /// 直接打印 Debug 级别日志（全局分类）。
    /// </summary>
    public static void LogDebug(string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger("Global").LogDebug(message, args);
    }
    
    /// <summary>
    /// 直接打印 Info 级别日志（全局分类）。
    /// </summary>
    public static void LogInfo(string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger("Global").LogInformation(message, args);
    }
    
    /// <summary>
    /// 直接打印 Debug 级别日志。
    /// </summary>
    public static void LogDebug<T>(T t , string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger<T>().LogDebug(message, args);
    }
    
    /// <summary>
    /// 直接打印 Info 级别日志。
    /// </summary>
    public static void LogInfo<T>(T t, string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger<T>().LogInformation(message, args);
    }
    
    /// <summary>
    /// 直接打印 Warning 级别日志。
    /// </summary>
    public static void LogWarning<T>(T t, string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger<T>().LogWarning(message, args);
    }
    
    /// <summary>
    /// 直接打印 Error 级别日志。
    /// </summary>
    public static void LogError<T>(T t, string message, params object[] args)
    {
        if (!IsInitialized)
        {
            // 未初始化时直接输出到 Unity Console，避免 EnsureReady 递归
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "[Global] (GameLogger Uninitialized) " + message, args);
            return;
        }
        _factory.CreateLogger<T>().LogError(message, args);
    }
    
    

    // ── 初始化 ─────────────────────────────────────────────────

    /// <summary>
    /// 显式初始化。可传入自定义配置；不传则使用默认配置。
    /// 幂等：已初始化时直接返回，不会重复初始化。
    /// </summary>
    /// <param name="configure">可选：自定义 ILoggingBuilder 配置</param>
    /// <exception cref="ObjectDisposedException">已销毁后禁止再初始化</exception>
    public static void Init(Action<ILoggingBuilder> configure = null)
    {
        // 快速路径：已 Ready，直接复用
        if (_state == State.Ready) return;

        lock (_lock)
        {
            switch (_state)
            {
                case State.Ready:       return;  // 双重检查
                case State.Initializing: return;  // 递归保护
                case State.Disposed:
                    throw new ObjectDisposedException(
                        nameof(GameLogger),
                        "[GameLogger] Cannot re-initialize after disposal.");
            }

            _state = State.Initializing;
            try
            {
                _factory = LoggerFactory.Create(logging =>
                {
                    logging.ClearProviders();

                    if (configure != null)
                        configure(logging);
                    else
                        ApplyDefaultProviders(logging);
                });

                _state = State.Ready;

                // 用刚建好的工厂记录自身初始化（不走缓存，避免 EnsureReady 递归）
                _factory.CreateLogger(nameof(GameLogger))
                        .LogInformation("[GameLogger] Initialized. Mode={0}",
                            configure != null ? "Custom" : "Default");
            }
            catch
            {
                _state = State.Uninitialized;  // 初始化失败，允许重试
                throw;
            }
        }
    }

    // ── 获取 Logger ────────────────────────────────────────────

    /// <summary>
    /// 获取或创建类型化 Logger。未初始化时自动懒加载默认配置。
    /// </summary>
    public static ILogger<T> Create<T>()
    {
        EnsureReady();
        string key = typeof(T).FullName;
        // GetOrAdd 原子操作：key 存在则返回缓存，否则创建并缓存
        return (ILogger<T>)_cache.GetOrAdd(key, _ => _factory.CreateLogger<T>());
    }

    /// <summary>
    /// 获取或创建字符串分类 Logger。未初始化时自动懒加载默认配置。
    /// </summary>
    public static ILogger Create(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category must not be null or whitespace.", nameof(category));

        EnsureReady();
        return _cache.GetOrAdd(category, k => _factory.CreateLogger(k));
    }

    // ── 销毁 ───────────────────────────────────────────────────

    /// <summary>
    /// 释放日志工厂及所有缓存。幂等：重复调用安全。
    /// 由 GameFramework.OnDestroy 调用。
    /// </summary>
    public static void Dispose()
    {
        if (_state == State.Disposed) return;

        lock (_lock)
        {
            if (_state == State.Disposed) return;

            // 先记录销毁日志（工厂还存活）
            if (_state == State.Ready)
                _factory?.CreateLogger(nameof(GameLogger))
                         .LogInformation("[GameLogger] Disposing...");

            _state = State.Disposed;
            _cache.Clear();
            _factory?.Dispose();
            _factory = null;
        }
    }

    // ── 内部工具 ───────────────────────────────────────────────

    /// <summary>
    /// 确保工厂处于 Ready 状态，否则自动懒初始化（降级为默认配置）。
    /// </summary>
    private static void EnsureReady()
    {
        if (_state == State.Ready) return;

        if (_state == State.Disposed)
            throw new ObjectDisposedException(
                nameof(GameLogger),
                "[GameLogger] Cannot create logger after disposal.");

        // 懒加载：调用方未显式 Init，自动用默认配置初始化
        Init();
    }

    /// <summary>
    /// 默认 Provider 配置：Unity Console + 按天滚动文件。
    /// </summary>
    private static void ApplyDefaultProviders(ILoggingBuilder logging)
    {
#if UNITY_EDITOR
        logging.SetMinimumLevel(LogLevel.Trace);
        logging.AddZLoggerUnityDebug();
#else
        logging.SetMinimumLevel(LogLevel.Information);
#endif
        logging.AddZLoggerRollingFile(options =>
        {
            options.FilePathSelector = (timestamp, seq) =>
                $"unity_logs/{timestamp.ToLocalTime():yyyy-MM-dd}_{seq:000}.log";
            options.RollingInterval = RollingInterval.Day;
            options.RollingSizeKB   = 1024 * 10;
        });
    }
}
