using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace UnitySystemArchitecture.Manager
{
    /// <summary>
    /// Unity系统管理器 - 核心单例
    /// 负责系统的注册、初始化、生命周期管理和更新调度
    /// </summary>
    public class SystemManager : MonoBehaviour, Core.ICoroutineRunner
    {
        // ==================== 单例 ====================
        private static SystemManager _instance;

        /// <summary>
        /// 获取SystemManager单例实例
        /// </summary>
        public static SystemManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("SystemManager未初始化！请确保场景中存在GameBootstrapper或SystemManager");
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ==================== 数据结构 ====================
        // 按注册顺序存储所有系统
        private readonly List<Core.ISystem> _systems = new List<Core.ISystem>();

        // 用于快速查找系统的字典
        private readonly Dictionary<Type, Core.ISystem> _systemMap = new Dictionary<Type, Core.ISystem>();

        // 记录已调用 OnStart 的系统
        private readonly HashSet<Core.ISystem> _startedSystems = new HashSet<Core.ISystem>();

        // ==================== 状态管理 ====================
        private bool _isGlobalPaused = false;
        private bool _isDisposed = false;

        // ==================== 属性 ====================

        /// <summary>
        /// 全局暂停状态
        /// </summary>
        public bool IsGlobalPaused
        {
            get => _isGlobalPaused;
        }

        /// <summary>
        /// 系统数量
        /// </summary>
        public int SystemCount => _systems.Count;

        /// <summary>
        /// 是否已销毁
        /// </summary>
        public bool IsDisposed => _isDisposed;

        // ==================== 日志委托 - 仅允许内部操作，外部不可直接订阅/移除 ====================
        private static event Action<string> OnLogInfo;
        private static event Action<string> OnLogWarning;
        private static event Action<string, Exception> OnLogError;

        /// <summary>
        /// 清空所有日志回调（外部不可直接操作事件，只能通过此方法清空）
        /// </summary>
        public static void ClearLogCallbacks()
        {
            OnLogInfo = null;
            OnLogWarning = null;
            OnLogError = null;
        }

        /// <summary>
        /// 设置统一 logger（覆盖原有回调）
        /// </summary>
        public static void SetLogger(object logger)
        {
            // 兼容 ILogger<SystemManager> 类型
            if (logger is Microsoft.Extensions.Logging.ILogger<SystemManager> ilogger)
            {
                OnLogInfo = msg => ilogger.LogInformation(msg);
                OnLogWarning = msg => ilogger.LogWarning(msg);
                OnLogError = (msg, ex) =>
                {
                    if (ex != null)
                        ilogger.LogError(ex, msg);
                    else
                        ilogger.LogError(msg);
                };
            }
        }

        // ==================== Unity生命周期 ====================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                LogInfo("[SystemManager] 单例已创建");
            }
            else
            {
                LogWarning("[SystemManager] 已存在实例，销毁重复对象");
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // 全局暂停时跳过所有Update
            if (_isGlobalPaused || _isDisposed) return;

            float deltaTime = Time.deltaTime;
            int count = _systems.Count;

            // 第一帧时调用 OnStart
            for (int i = 0; i < count; i++)
            {
                var system = _systems[i];
                if (!_startedSystems.Contains(system) && system is Core.IStart startable)
                {
                    try
                    {
                        startable.OnStart();
                        _startedSystems.Add(system);
                    }
                    catch (Exception e)
                    {
                        LogError($"[SystemManager] 系统 {system.GetType().Name} OnStart执行失败", e);
                    }
                }
            }
            // 调用 OnTick
            for (int i = 0; i < count; i++)
            {
                var system = _systems[i];

                // 检查单个系统是否实现了IPausable且处于暂停状态
                if (system is Core.IPausable pausable && pausable.IsPaused)
                {
                    continue;
                }

                if (system is Core.ITick tickable)
                {
                    try
                    {
                        tickable.OnTick(deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[SystemManager] 系统 {system.GetType().Name} OnTick执行失败", e);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            // 全局暂停时跳过所有FixedUpdate
            if (_isGlobalPaused || _isDisposed) return;

            float fixedDeltaTime = Time.fixedDeltaTime;
            int count = _systems.Count;

            for (int i = 0; i < count; i++)
            {
                var system = _systems[i];

                // 检查单个系统是否实现了IPausable且处于暂停状态
                if (system is Core.IPausable pausable && pausable.IsPaused)
                {
                    continue;
                }

                if (system is Core.IFixedTick fixedTickable)
                {
                    try
                    {
                        fixedTickable.OnFixedTick(fixedDeltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[SystemManager] 系统 {system.GetType().Name} OnFixedTick执行失败", e);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // 全局暂停时跳过所有LateUpdate
            if (_isGlobalPaused || _isDisposed) return;

            float deltaTime = Time.deltaTime;
            int count = _systems.Count;

            for (int i = 0; i < count; i++)
            {
                var system = _systems[i];

                // 检查单个系统是否实现了IPausable且处于暂停状态
                if (system is Core.IPausable pausable && pausable.IsPaused)
                {
                    continue;
                }

                if (system is Core.ILateTick lateTickable)
                {
                    try
                    {
                        lateTickable.OnLateTick(deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[SystemManager] 系统 {system.GetType().Name} OnLateTick执行失败", e);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DisposeAll();
            _instance = null;
            LogInfo("[SystemManager] 已销毁");
        }

        // ==================== 系统注册 ====================

        /// <summary>
        /// 注册系统到管理器（按调用顺序加入列表）
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        /// <param name="system">系统实例</param>
        /// <returns>返回注册的系统实例</returns>
        public T Register<T>(T system) where T : Core.ISystem
        {
            if (_isDisposed)
            {
                LogWarning($"[SystemManager] 系统已销毁，无法注册新系统: {typeof(T).Name}");
                return system;
            }

            var type = typeof(T);

            if (_systemMap.ContainsKey(type))
            {
                LogWarning($"[SystemManager] 系统 {type.Name} 已存在，将被覆盖");
                var existingSystem = _systemMap[type];
                _systems.Remove(existingSystem);
                _startedSystems.Remove(existingSystem);
            }

            _systemMap[type] = system;
            _systems.Add(system);

            try
            {
                system.OnInit();
                LogInfo($"[SystemManager] 系统 {type.Name} 已注册");
            }
            catch (Exception e)
            {
                LogError($"[SystemManager] 系统 {type.Name} 初始化失败", e);
            }

            return system;
        }

        /// <summary>
        /// 注销系统
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        public void Unregister<T>() where T : Core.ISystem
        {
            var type = typeof(T);

            if (_systemMap.TryGetValue(type, out var system))
            {
                try
                {
                    system.OnDispose();
                }
                catch (Exception e)
                {
                    LogError($"[SystemManager] 系统 {type.Name} 销毁失败", e);
                }

                _systems.Remove(system);
                _systemMap.Remove(type);
                _startedSystems.Remove(system);

                LogInfo($"[SystemManager] 系统 {type.Name} 已注销");
            }
        }

        /// <summary>
        /// 注销指定系统
        /// </summary>
        /// <param name="system">系统实例</param>
        public void Unregister(Core.ISystem system)
        {
            if (system == null) return;

            var type = system.GetType();

            if (_systems.Contains(system))
            {
                try
                {
                    system.OnDispose();
                }
                catch (Exception e)
                {
                    LogError($"[SystemManager] 系统 {type.Name} 销毁失败", e);
                }

                _systems.Remove(system);
                _systemMap.Remove(type);
                _startedSystems.Remove(system);

                LogInfo($"[SystemManager] 系统 {type.Name} 已注销");
            }
        }

        // ==================== 系统获取 ====================

        /// <summary>
        /// 获取系统实例
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        /// <returns>系统实例，如果不存在则返回null</returns>
        public T Get<T>() where T : class, Core.ISystem
        {
            var type = typeof(T);

            if (_systemMap.TryGetValue(type, out var system))
            {
                return system as T;
            }

            LogWarning($"[SystemManager] 未找到系统: {type.Name}");
            return null;
        }

        /// <summary>
        /// 尝试获取系统
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        /// <param name="system">输出参数，系统实例</param>
        /// <returns>是否成功获取</returns>
        public bool TryGet<T>(out T system) where T : class, Core.ISystem
        {
            system = Get<T>();
            return system != null;
        }

        /// <summary>
        /// 检查系统是否已注册
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        /// <returns>是否已注册</returns>
        public bool IsRegistered<T>() where T : Core.ISystem
        {
            return _systemMap.ContainsKey(typeof(T));
        }

        // ==================== 暂停控制 ====================

        /// <summary>
        /// 设置全局暂停状态
        /// </summary>
        /// <param name="isPaused">是否暂停</param>
        public void SetGlobalPause(bool isPaused)
        {
            if (_isGlobalPaused == isPaused) return;

            _isGlobalPaused = isPaused;
            LogInfo($"[SystemManager] 全局暂停状态: {(_isGlobalPaused ? "已暂停" : "已恢复")}");
        }

        /// <summary>
        /// 切换全局暂停状态
        /// </summary>
        public void ToggleGlobalPause()
        {
            SetGlobalPause(!_isGlobalPaused);
        }

        /// <summary>
        /// 设置单个系统的暂停状态
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        /// <param name="isPaused">是否暂停</param>
        public void SetSystemPause<T>(bool isPaused) where T : class, Core.ISystem
        {
            var system = Get<T>();

            if (system == null)
            {
                LogWarning($"[SystemManager] 无法暂停不存在的系统: {typeof(T).Name}");
                return;
            }

            if (system is Core.IPausable pausable)
            {
                if (pausable.IsPaused != isPaused)
                {
                    pausable.IsPaused = isPaused;

                    if (isPaused)
                    {
                        pausable.OnPause();
                        LogInfo($"[SystemManager] 系统 {typeof(T).Name} 已暂停");
                    }
                    else
                    {
                        pausable.OnResume();
                        LogInfo($"[SystemManager] 系统 {typeof(T).Name} 已恢复");
                    }
                }
            }
            else
            {
                LogWarning($"[SystemManager] 系统 {typeof(T).Name} 未实现IPausable接口，无法单独暂停");
            }
        }

        /// <summary>
        /// 切换单个系统的暂停状态
        /// </summary>
        /// <typeparam name="T">系统类型</typeparam>
        public void ToggleSystemPause<T>() where T : class, Core.ISystem
        {
            var system = Get<T>();

            if (system is Core.IPausable pausable)
            {
                SetSystemPause<T>(!pausable.IsPaused);
            }
        }

        // ==================== 协程代理 ====================

        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            return base.StartCoroutine(routine);
        }

        public new void StopCoroutine(Coroutine routine)
        {
            base.StopCoroutine(routine);
        }

        // ==================== 生命周期控制 ====================

        /// <summary>
        /// 销毁所有系统（按注册顺序的反向顺序）
        /// </summary>
        public void DisposeAll()
        {
            if (_isDisposed) return;

            LogInfo("[SystemManager] 开始销毁所有系统...");

            // 按注册顺序的反向顺序销毁（后注册先销毁）
            for (int i = _systems.Count - 1; i >= 0; i--)
            {
                try
                {
                    _systems[i].OnDispose();
                }
                catch (Exception e)
                {
                    LogError($"[SystemManager] 系统 {_systems[i].GetType().Name} 销毁失败", e);
                }
            }

            _systems.Clear();
            _systemMap.Clear();
            _startedSystems.Clear();
            _isDisposed = true;

            LogInfo("[SystemManager] 所有系统已销毁");
        }

        /// <summary>
        /// 获取所有已注册的系统类型
        /// </summary>
        /// <returns>系统类型枚举</returns>
        public IEnumerable<Type> GetAllSystemTypes()
        {
            return _systemMap.Keys;
        }

        // ==================== 内部日志方法 ====================

        private void LogInfo(string message)
        {
            if (OnLogInfo != null)
            {
                OnLogInfo(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private void LogWarning(string message)
        {
            if (OnLogWarning != null)
            {
                OnLogWarning(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        private void LogError(string message, Exception exception = null)
        {
            if (OnLogError != null)
            {
                OnLogError(message, exception);
            }
            else
            {
                if (exception != null)
                {
                    Debug.LogError($"{message}\n{exception}");
                }
                else
                {
                    Debug.LogError(message);
                }
            }
        }
        
        /// <summary>
        // 原有分级回调方法已整合为 SetLogger
    }
}
