using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace UnityDependencyInjection
{
    /// <summary>
    /// 依赖管理器 - 核心单例
    /// 负责模块的注册、初始化、生命周期管理和更新调度
    /// </summary>
    public class DependencyManager : MonoBehaviour, ICoroutineRunner
    {
        // ==================== 单例 ====================
        private static DependencyManager _instance;

        /// <summary>
        /// 获取DependencyManager单例实例
        /// </summary>
        public static DependencyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("DependencyManager未初始化！请确保场景中存在GameBootstrapper或DependencyManager");
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ==================== 数据结构 ====================
        // 按注册顺序存储所有模块
        private readonly List<IInitializable> _modules = new List<IInitializable>();

        // 用于快速查找模块的字典
        private readonly Dictionary<Type, IInitializable> _moduleMap = new Dictionary<Type, IInitializable>();

        // 记录已调用 OnStart 的模块
        private readonly HashSet<IInitializable> _startedModules = new HashSet<IInitializable>();

        // ==================== 状态管理 ====================
        private bool _isGlobalPaused = false;
        private bool _isDisposed = false;

        // ==================== 属性 ====================

        /// <summary>
        /// 全局暂停状态
        /// </summary>
        public bool IsGlobalPaused => _isGlobalPaused;

        /// <summary>
        /// 已注册模块数量
        /// </summary>
        public int ModuleCount => _modules.Count;

        /// <summary>
        /// 是否已销毁
        /// </summary>
        public bool IsDisposed => _isDisposed;

        // ==================== 日志委托 ====================
        private static event Action<string> OnLogInfo;
        private static event Action<string> OnLogWarning;
        private static event Action<string, Exception> OnLogError;

        /// <summary>
        /// 清空所有日志回调
        /// </summary>
        public static void ClearLogCallbacks()
        {
            OnLogInfo = null;
            OnLogWarning = null;
            OnLogError = null;
        }

        /// <summary>
        /// 设置统一 logger
        /// </summary>
        public static void SetLogger(object logger)
        {
            if (logger is ILogger<DependencyManager> ilogger)
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
                LogInfo("[DependencyManager] 单例已创建");
            }
            else
            {
                LogWarning("[DependencyManager] 已存在实例，销毁重复对象");
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_isGlobalPaused || _isDisposed) return;

            float deltaTime = Time.deltaTime;
            int count = _modules.Count;

            // 第一帧调用 OnStart
            for (int i = 0; i < count; i++)
            {
                var module = _modules[i];
                if (!_startedModules.Contains(module) && module is IStartable startable)
                {
                    try
                    {
                        startable.OnStart();
                        _startedModules.Add(module);
                    }
                    catch (Exception e)
                    {
                        LogError($"[DependencyManager] 模块 {module.GetType().Name} OnStart执行失败", e);
                    }
                }
            }

            // 调用 OnTick
            for (int i = 0; i < count; i++)
            {
                var module = _modules[i];
                if (module is IPausable pausable && pausable.IsPaused) continue;
                if (module is ITickable tickable)
                {
                    try
                    {
                        tickable.OnTick(deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[DependencyManager] 模块 {module.GetType().Name} OnTick执行失败", e);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (_isGlobalPaused || _isDisposed) return;

            float fixedDeltaTime = Time.fixedDeltaTime;
            int count = _modules.Count;

            for (int i = 0; i < count; i++)
            {
                var module = _modules[i];
                if (module is IPausable pausable && pausable.IsPaused) continue;
                if (module is IFixedTickable fixedTickable)
                {
                    try
                    {
                        fixedTickable.OnFixedTick(fixedDeltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[DependencyManager] 模块 {module.GetType().Name} OnFixedTick执行失败", e);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_isGlobalPaused || _isDisposed) return;

            float deltaTime = Time.deltaTime;
            int count = _modules.Count;

            for (int i = 0; i < count; i++)
            {
                var module = _modules[i];
                if (module is IPausable pausable && pausable.IsPaused) continue;
                if (module is ILateTickable lateTickable)
                {
                    try
                    {
                        lateTickable.OnLateTick(deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[DependencyManager] 模块 {module.GetType().Name} OnLateTick执行失败", e);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DisposeAll();
            _instance = null;
            LogInfo("[DependencyManager] 已销毁");
        }

        // ==================== 模块注册 ====================

        /// <summary>
        /// 注册模块到管理器
        /// </summary>
        public T Register<T>(T module) where T : IInitializable
        {
            if (_isDisposed)
            {
                LogWarning($"[DependencyManager] 管理器已销毁，无法注册模块: {typeof(T).Name}");
                return module;
            }

            var type = typeof(T);

            if (_moduleMap.ContainsKey(type))
            {
                LogWarning($"[DependencyManager] 模块 {type.Name} 已存在，将被覆盖");
                var existing = _moduleMap[type];
                _modules.Remove(existing);
                _startedModules.Remove(existing);
            }

            _moduleMap[type] = module;
            _modules.Add(module);

            try
            {
                module.OnInit();
                LogInfo($"[DependencyManager] 模块 {type.Name} 已注册");
            }
            catch (Exception e)
            {
                LogError($"[DependencyManager] 模块 {type.Name} 初始化失败", e);
            }

            return module;
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public void UnRegister<T>() where T : IInitializable
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var module))
            {
                UnRegisterInternal(module);
            }
        }

        /// <summary>
        /// 注销指定模块实例
        /// </summary>
        public void UnRegister(IInitializable module)
        {
            if (module == null) return;
            if (_modules.Contains(module))
            {
                UnRegisterInternal(module);
            }
        }

        private void UnRegisterInternal(IInitializable module)
        {
            var type = module.GetType();
            try
            {
                module.OnDispose();
            }
            catch (Exception e)
            {
                LogError($"[DependencyManager] 模块 {type.Name} 销毁失败", e);
            }

            _modules.Remove(module);
            _moduleMap.Remove(type);
            _startedModules.Remove(module);
            LogInfo($"[DependencyManager] 模块 {type.Name} 已注销");
        }

        // ==================== 模块获取 ====================

        /// <summary>
        /// 获取模块实例
        /// </summary>
        public T Get<T>() where T : class, IInitializable
        {
            var type = typeof(T);
            if (_moduleMap.TryGetValue(type, out var module))
            {
                return module as T;
            }
            LogWarning($"[DependencyManager] 未找到模块: {type.Name}");
            return null;
        }

        /// <summary>
        /// 尝试获取模块
        /// </summary>
        public bool TryGet<T>(out T module) where T : class, IInitializable
        {
            module = Get<T>();
            return module != null;
        }

        /// <summary>
        /// 检查模块是否已注册
        /// </summary>
        public bool IsRegistered<T>() where T : IInitializable
        {
            return _moduleMap.ContainsKey(typeof(T));
        }

        // ==================== 暂停控制 ====================

        /// <summary>
        /// 设置全局暂停状态
        /// </summary>
        public void SetGlobalPause(bool isPaused)
        {
            if (_isGlobalPaused == isPaused) return;
            _isGlobalPaused = isPaused;
            LogInfo($"[DependencyManager] 全局暂停状态: {(_isGlobalPaused ? "已暂停" : "已恢复")}");
        }

        /// <summary>
        /// 切换全局暂停状态
        /// </summary>
        public void ToggleGlobalPause() => SetGlobalPause(!_isGlobalPaused);

        /// <summary>
        /// 设置单个模块的暂停状态
        /// </summary>
        public void SetModulePause<T>(bool isPaused) where T : class, IInitializable
        {
            var module = Get<T>();
            if (module == null)
            {
                LogWarning($"[DependencyManager] 无法暂停不存在的模块: {typeof(T).Name}");
                return;
            }

            if (module is IPausable pausable)
            {
                if (pausable.IsPaused != isPaused)
                {
                    pausable.IsPaused = isPaused;
                    if (isPaused)
                    {
                        pausable.OnPause();
                        LogInfo($"[DependencyManager] 模块 {typeof(T).Name} 已暂停");
                    }
                    else
                    {
                        pausable.OnResume();
                        LogInfo($"[DependencyManager] 模块 {typeof(T).Name} 已恢复");
                    }
                }
            }
            else
            {
                LogWarning($"[DependencyManager] 模块 {typeof(T).Name} 未实现IPausable接口");
            }
        }

        /// <summary>
        /// 切换单个模块的暂停状态
        /// </summary>
        public void ToggleModulePause<T>() where T : class, IInitializable
        {
            if (Get<T>() is IPausable pausable)
            {
                SetModulePause<T>(!pausable.IsPaused);
            }
        }

        // ==================== 协程代理 ====================

        public new Coroutine StartCoroutine(IEnumerator routine) => base.StartCoroutine(routine);
        public new void StopCoroutine(Coroutine routine) => base.StopCoroutine(routine);

        // ==================== 生命周期控制 ====================

        /// <summary>
        /// 销毁所有模块（反向注销）
        /// </summary>
        public void DisposeAll()
        {
            if (_isDisposed) return;
            LogInfo("[DependencyManager] 开始销毁所有模块...");

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    _modules[i].OnDispose();
                }
                catch (Exception e)
                {
                    LogError($"[DependencyManager] 模块 {_modules[i].GetType().Name} 销毁失败", e);
                }
            }

            _modules.Clear();
            _moduleMap.Clear();
            _startedModules.Clear();
            _isDisposed = true;
            LogInfo("[DependencyManager] 所有模块已销毁");
        }

        /// <summary>
        /// 获取所有已注册模块类型
        /// </summary>
        public IEnumerable<Type> GetAllModuleTypes() => _moduleMap.Keys;

        // ==================== 内部日志方法 ====================

        private void LogInfo(string msg)
        {
            if (OnLogInfo != null) OnLogInfo(msg);
            else Debug.Log(msg);
        }

        private void LogWarning(string msg)
        {
            if (OnLogWarning != null) OnLogWarning(msg);
            else Debug.LogWarning(msg);
        }

        private void LogError(string msg, Exception ex = null)
        {
            if (OnLogError != null) OnLogError(msg, ex);
            else Debug.LogError(ex != null ? $"{msg}\n{ex}" : msg);
        }
    }
}

namespace UnityDependencyInjection
{
    public interface IInitializable { void OnInit();  void OnDispose();}
    public interface IStartable { void OnStart(); }
    public interface ITickable { void OnTick(float deltaTime); }
    public interface IFixedTickable { void OnFixedTick(float fixedDeltaTime); }
    public interface ILateTickable { void OnLateTick(float deltaTime); }
    public interface IPausable { bool IsPaused { get; set; } void OnPause(); void OnResume(); }
    public interface IDisposable { void OnDispose(); }
    public interface ICoroutineRunner { } // 保持原有定义
}