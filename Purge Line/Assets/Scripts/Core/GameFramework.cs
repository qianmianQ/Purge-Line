using System;
using UnityEngine;
using UnitySystemArchitecture.Manager;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;
using ZLogger.Unity;

/// <summary>
/// 游戏框架：全局单例，负责创建 SystemManager 并注册所有框架系统
///
/// 启动流程：
///   GameBootstrapper.Awake()
///     → GameFramework.Awake()  — 创建 SystemManager
///     → GameFramework.Initialize() — 注册所有系统，框架正式运行
/// </summary>
public class GameFramework : MonoBehaviour
{
    public enum GameState
    {
        None,
    }

    public static GameFramework Instance { get; private set; }

    public GameState State { get; private set; } = GameState.None;

    private static ILogger<GameFramework> _logger;

    // 启动时间戳，用于日志文件唯一命名
    private static readonly DateTime _startupTime = DateTime.Now;

    // ── Unity 生命周期 ─────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化全局日志工厂（自定义配置）
        GameLogger.Init(logging =>
        {
#if UNITY_EDITOR
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddZLoggerUnityDebug();
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, seq) =>
                    $"../unity_logs/{_startupTime:yyyy-MM-dd_HH-mm-ss}_{seq:000}.log";
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB   = 1024 * 10;
                options.UseJsonFormatter();
            });
#else
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, seq) =>
                    $"game_logs/{_startupTime:yyyy-MM-dd_HH-mm-ss}_{seq:000}.log";
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB   = 1024 * 10;
                options.UseJsonFormatter();
            });
#endif
        });
        _logger = GameLogger.Create<GameFramework>();

        _logger.LogInformation("Framework starting...");

        // 创建 SystemManager（与 GameFramework 同生命周期的独立 GameObject）
        var smGo = new GameObject("[SystemManager]")
        {
            transform =
            {
                parent = transform.parent
            }
        };
        var sm = smGo.AddComponent<SystemManager>(); // 立即触发 SystemManager.Awake()，设置单例

        // 连接 SystemManager 的日志到 GameLogger
        var smLogger = GameLogger.Create<SystemManager>();
        SystemManager.SetLogger(smLogger);

        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        _logger.LogInformation("Framework destroyed");
        GameLogger.Dispose();
        Instance = null;
    }

    // ── 公开：系统注册入口（由 GameBootstrapper 调用）────────────

    /// <summary>
    /// 按顺序注册所有框架系统并启动
    /// 注意：所有系统依赖均在各自的 OnStart() 中延迟获取，此处只关注注册顺序
    /// </summary>
    public void Initialize()
    {
        var sm = SystemManager.Instance;

        _logger.LogInformation("All systems registered, framework running");
    }

    // ── 内部工具 ──────────────────────────────────────────────

    private void SetState(GameState newState)
    {
        _logger.LogDebug("State: {0} -> {1}", State, newState);
        State = newState;
    }
}
