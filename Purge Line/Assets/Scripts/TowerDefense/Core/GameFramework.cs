using System;
using Base.BaseSystem.EventSystem;
using Microsoft.Extensions.Logging;
using PurgeLine.Events;
using PurgeLine.Resource;
using PurgeLine.Resource.Internal;
using TowerDefense.Bridge;
using TowerDefense.Data;
using TowerDefense.ECS.Bridge;
using TowerDefense.ECS.Lifecycle;
using UnityEngine;
using VContainer;
using ZLogger;
using ZLogger.Providers;
using ZLogger.Unity;

/// <summary>
/// 游戏框架：负责初始化日志/事件系统并驱动启动流程
///
/// 启动流程：
///   GameBootstrapper.Awake()
///     → GameFramework.Awake()  — 创建 VContainer LifetimeScope
///     → GameFramework.Initialize() — 校验容器，框架正式运行
/// </summary>
public class GameFramework : MonoBehaviour
{
    public enum GameState
    {
        None,
    }

    public GameState State { get; private set; } = GameState.None;

    private static ILogger<GameFramework> _logger;

    // 启动时间戳，用于日志文件唯一命名
    private static readonly DateTime _startupTime = DateTime.Now;
    
    private ResourceManager _resourceManager;
    
    private IObjectResolver _resolver;

    [Header("Manual Start (Debug)")]
    [SerializeField] private bool autoStartInPlayMode = true;
    [SerializeField] private string autoStartLevelId = "level_01";
    [SerializeField] private float autoStartDelaySeconds = 3f;

    private bool _visualPoolsInitialized;

    // ── Unity 生命周期 ─────────────────────────────────────────

    private void Awake()
    {
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

        // 预先创建 ResourceManager 实例，确保在任何系统需要时都可用
        _resourceManager = new ResourceManager(ResourceManagerConfig.Default);
        _logger.LogInformation("ResourceManager initialized");
        
        EventManager.Init(typeof(UIEvent), typeof(GamePlayEvent), typeof(GlobalEvent));
        _logger.LogInformation("Event system initialized");
        
        var lifetimeScopeGo = new GameObject("[GameLifetimeScope]")
        {
            transform =
            {
                parent = transform.parent
            }
        };
        var lifetimeScope = lifetimeScopeGo.AddComponent<GameLifetimeScope>();
        _resolver = lifetimeScope.Container;

        Initialize();
    }

    private void Start()
    {
        EnsureVisualPoolsInitialized();

        // 正常流程由关卡选择 UI 调用 StartGameSession。
        if (autoStartInPlayMode)
        {
            Invoke(nameof(StartAutoConfiguredGame), autoStartDelaySeconds);
        }
    }

    private void StartAutoConfiguredGame()
    {
        StartGameSession(autoStartLevelId);
    }

    public bool StartGameSession(string levelId)
    {
        if (_resolver == null)
        {
            _logger.LogError("Cannot start game session: resolver is null");
            return false;
        }

        if (string.IsNullOrWhiteSpace(levelId))
        {
            _logger.LogError("Cannot start game session: levelId is empty");
            return false;
        }

        EnsureVisualPoolsInitialized();

        var lifecycle = _resolver.Resolve<IEcsLifecycleService>();
        if (!lifecycle.StartWorld())
        {
            _logger.LogError("Failed to start ECS world");
            return false;
        }

        bool loaded = _resolver.Resolve<IGridBridgeSystem>().LoadLevel(levelId);
        if (!loaded)
        {
            _logger.LogError("Failed to load level {0}, stopping world", levelId);
            lifecycle.StopWorld();
            return false;
        }

        _logger.LogInformation("Game session started with level {0}", levelId);
        return true;
    }

    public void StopGameSession()
    {
        if (_resolver == null)
            return;

        _resolver.Resolve<IEcsLifecycleService>().StopWorld();
        _logger.LogInformation("Game session stopped");
    }

    public bool TryGetEcsLifecycleService(out IEcsLifecycleService lifecycleService)
    {
        lifecycleService = null;
        if (_resolver == null)
            return false;

        lifecycleService = _resolver.Resolve<IEcsLifecycleService>();
        return lifecycleService != null;
    }

    public bool TryGetGridBridgeSystem(out IGridBridgeSystem gridBridgeSystem)
    {
        gridBridgeSystem = null;
        if (_resolver == null)
            return false;

        gridBridgeSystem = _resolver.Resolve<IGridBridgeSystem>();
        return gridBridgeSystem != null;
    }

    private void OnDestroy()
    {
        StopGameSession();
        _logger.LogInformation("Framework destroyed");
        EventManager.Dispose();
        GameLogger.Dispose();
    }

    /// <summary>
    /// 校验框架容器是否构建完成
    /// </summary>
    private void Initialize()
    {
        if (_resolver == null)
        {
            _logger.LogError("VContainer resolver is null, framework cannot continue");
            return;
        }

        _logger.LogInformation("VContainer ready, framework running");
    }

    private void EnsureVisualPoolsInitialized()
    {
        if (_visualPoolsInitialized || _resolver == null)
            return;

        _resolver.Resolve<IEcsVisualBridgeSystem>().InitEntitiesPools(new[]
        {
            CombatConfig.TowerPrefabAddress,
            CombatConfig.BulletPrefabAddress,
            CombatConfig.EnemyPrefabAddress
        });

        _visualPoolsInitialized = true;
    }
    
    public ResourceManager GetResourceManager()
    {
        _resourceManager ??= new ResourceManager(ResourceManagerConfig.Default);
        if (_resourceManager == null)
        {
            throw new InvalidOperationException("Failed to create ResourceManager");
        }
        return _resourceManager;
    }
}
