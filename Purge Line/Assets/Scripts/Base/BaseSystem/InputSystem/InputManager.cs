// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Microsoft.Extensions.Logging;
// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.InputSystem.LowLevel;
// using UnityDependencyInjection;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 工业级响应式输入系统核心管理器
//     /// 基于 Unity InputSystem 和 R3 响应式编程库
//     /// 支持输入上下文栈、设备管理、本地多人、复合手势等高级功能
//     /// </summary>
//     public class InputManager : IInitializable, IStartable, ITickable, IDisposable
//     {
//         // ==================== 单例实例 ====================
//         private static InputManager _instance;
//         public static InputManager Instance => _instance;
//
//         // ==================== 日志 ====================
//         private static ILogger _logger;
//
//         // ==================== 核心组件 ====================
//         private InputContextStack _contextStack;
//         private DeviceManager _deviceManager;
//         private InputConfig _config;
//
//         // ==================== 输入动作集合 ====================
//         private readonly Dictionary<Type, object> _actionMaps = new Dictionary<Type, object>();
//
//         // ==================== 多玩家输入 ====================
//         private readonly Dictionary<int, PlayerInputWrapper> _playerInputs = new Dictionary<int, PlayerInputWrapper>();
//
//         // ==================== 响应式事件 ====================
//         private readonly Subject<InputDevice> _onDeviceConnected = new Subject<InputDevice>();
//         private readonly Subject<InputDevice> _onDeviceDisconnected = new Subject<InputDevice>();
//         private readonly Subject<InputContextBase> _onContextActivated = new Subject<InputContextBase>();
//         private readonly Subject<InputContextBase> _onContextDeactivated = new Subject<InputContextBase>();
//
//         // ==================== 状态 ====================
//         private bool _initialized;
//         private bool _started;
//
//         // ==================== 公开属性 ====================
//         public InputContextStack ContextStack => _contextStack;
//         public DeviceManager DeviceManager => _deviceManager;
//         public InputConfig Config => _config;
//         public bool IsInitialized => _initialized;
//
//         // ==================== 响应式事件流 ====================
//         public Observable<InputDevice> OnDeviceConnected => _onDeviceConnected.AsObservable();
//         public Observable<InputDevice> OnDeviceDisconnected => _onDeviceDisconnected.AsObservable();
//         public Observable<InputContextBase> OnContextActivated => _onContextActivated.AsObservable();
//         public Observable<InputContextBase> OnContextDeactivated => _onContextDeactivated.AsObservable();
//
//         // ==================== IInitializable ====================
//
//         public void OnInit()
//         {
//             if (_instance != null)
//             {
//                 Debug.LogError("[InputManager] 已存在实例，重复创建被阻止");
//                 return;
//             }
//
//             _instance = this;
//             _logger = GameLogger.Create("InputManager");
//
//             try
//             {
//                 // 初始化配置
//                 _config = InputConfig.LoadDefault();
//
//                 // 初始化核心组件
//                 _contextStack = new InputContextStack(this);
//                 _deviceManager = new DeviceManager(this);
//
//                 // 注册默认输入上下文
//                 RegisterDefaultContexts();
//
//                 // 监听设备连接/断开
//                 InputSystem.onDeviceChange += OnDeviceChange;
//
//                 _initialized = true;
//                 _logger.LogInformation("[InputManager] 初始化成功");
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"[InputManager] 初始化失败: {ex}");
//             }
//         }
//
//         public void OnDispose()
//         {
//             if (!_initialized) return;
//
//             try
//             {
//                 InputSystem.onDeviceChange -= OnDeviceChange;
//
//                 // 清理玩家输入
//                 foreach (var playerInput in _playerInputs.Values)
//                 {
//                     playerInput.Dispose();
//                 }
//                 _playerInputs.Clear();
//
//                 // 清理输入动作
//                 foreach (var actionMap in _actionMaps.Values)
//                 {
//                     if (actionMap is IDisposable disposable)
//                     {
//                         disposable.Dispose();
//                     }
//                 }
//                 _actionMaps.Clear();
//
//                 // 清理组件
//                 _contextStack.Dispose();
//                 _deviceManager.Dispose();
//
//                 // 清理事件
//                 _onDeviceConnected.Dispose();
//                 _onDeviceDisconnected.Dispose();
//                 _onContextActivated.Dispose();
//                 _onContextDeactivated.Dispose();
//
//                 _instance = null;
//                 _initialized = false;
//                 _logger.LogInformation("[InputManager] 已销毁");
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"[InputManager] 销毁失败: {ex}");
//             }
//         }
//
//         // ==================== IStartable ====================
//
//         public void OnStart()
//         {
//             if (!_initialized)
//             {
//                 _logger.LogWarning("[InputManager] 未初始化，无法启动");
//                 return;
//             }
//
//             try
//             {
//                 // 启用默认输入上下文
//                 _contextStack.PushContext<GlobalInputContext>();
//
//                 // 初始化设备状态
//                 _deviceManager.RefreshDevices();
//
//                 _started = true;
//                 _logger.LogInformation("[InputManager] 启动成功");
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"[InputManager] 启动失败: {ex}");
//             }
//         }
//
//         // ==================== ITickable ====================
//
//         public void OnTick(float deltaTime)
//         {
//             if (!_initialized || !_started) return;
//
//             try
//             {
//                 // 更新输入状态
//                 InputSystem.Update();
//
//                 // 更新设备管理器
//                 _deviceManager.Update();
//
//                 // 更新上下文栈
//                 _contextStack.Update(deltaTime);
//
//                 // 更新多玩家输入
//                 foreach (var playerInput in _playerInputs.Values)
//                 {
//                     playerInput.Update(deltaTime);
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"[InputManager] 更新失败: {ex}");
//             }
//         }
//
//         // ==================== 输入动作管理 ====================
//
//         /// <summary>
//         /// 获取或创建输入动作集合
//         /// </summary>
//         public TActions GetActionMap<TActions>() where TActions : IInputActionCollection2, new()
//         {
//             var type = typeof(TActions);
//
//             if (_actionMaps.TryGetValue(type, out var existing))
//             {
//                 return (TActions)existing;
//             }
//
//             var actions = new TActions();
//             actions.Enable();
//             _actionMaps[type] = actions;
//             _logger.LogDebug($"[InputManager] 输入动作集合已创建: {type.Name}");
//
//             return actions;
//         }
//
//         /// <summary>
//         /// 获取输入动作的 Observable 流
//         /// </summary>
//         public Observable<InputAction.CallbackContext> GetAction<TActions>(Func<TActions, InputAction> selector)
//             where TActions : IInputActionCollection2, new()
//         {
//             var actionMap = GetActionMap<TActions>();
//             var action = selector(actionMap);
//
//             var subject = new Subject<InputAction.CallbackContext>();
//             var performedHandle = action.performed += ctx => subject.OnNext(ctx);
//             var canceledHandle = action.canceled += ctx => subject.OnNext(ctx);
//
//             return subject.AsObservable()
//                 .Finally(() =>
//                 {
//                     action.performed -= performedHandle;
//                     action.canceled -= canceledHandle;
//                 });
//         }
//
//         // ==================== 多玩家输入管理 ====================
//
//         /// <summary>
//         /// 创建玩家输入
//         /// </summary>
//         public PlayerInputWrapper CreatePlayerInput(int playerIndex)
//         {
//             if (_playerInputs.ContainsKey(playerIndex))
//             {
//                 _logger.LogWarning($"[InputManager] 玩家输入已存在: {playerIndex}");
//                 return _playerInputs[playerIndex];
//             }
//
//             var playerInput = new PlayerInputWrapper(playerIndex);
//             _playerInputs[playerIndex] = playerInput;
//
//             _logger.LogInformation($"[InputManager] 玩家输入已创建: {playerIndex}");
//             return playerInput;
//         }
//
//         /// <summary>
//         /// 获取玩家输入
//         /// </summary>
//         public PlayerInputWrapper GetPlayerInput(int playerIndex)
//         {
//             _playerInputs.TryGetValue(playerIndex, out var playerInput);
//             return playerInput;
//         }
//
//         /// <summary>
//         /// 移除玩家输入
//         /// </summary>
//         public void RemovePlayerInput(int playerIndex)
//         {
//             if (_playerInputs.TryGetValue(playerIndex, out var playerInput))
//             {
//                 playerInput.Dispose();
//                 _playerInputs.Remove(playerIndex);
//                 _logger.LogInformation($"[InputManager] 玩家输入已移除: {playerIndex}");
//             }
//         }
//
//         // ==================== 设备管理 ====================
//
//         private void OnDeviceChange(InputDevice device, InputDeviceChange change)
//         {
//             switch (change)
//             {
//                 case InputDeviceChange.Added:
//                     _logger.LogDebug($"[InputManager] 设备连接: {device.name} ({device.GetType().Name})");
//                     _onDeviceConnected.OnNext(device);
//                     _deviceManager.AddDevice(device);
//                     break;
//
//                 case InputDeviceChange.Removed:
//                     _logger.LogDebug($"[InputManager] 设备断开: {device.name}");
//                     _onDeviceDisconnected.OnNext(device);
//                     _deviceManager.RemoveDevice(device);
//                     break;
//
//                 case InputDeviceChange.Enabled:
//                     _logger.LogDebug($"[InputManager] 设备启用: {device.name}");
//                     _deviceManager.SetDeviceEnabled(device, true);
//                     break;
//
//                 case InputDeviceChange.Disabled:
//                     _logger.LogDebug($"[InputManager] 设备禁用: {device.name}");
//                     _deviceManager.SetDeviceEnabled(device, false);
//                     break;
//             }
//         }
//
//         // ==================== 上下文管理 ====================
//
//         internal void NotifyContextActivated(InputContextBase context)
//         {
//             _onContextActivated.OnNext(context);
//         }
//
//         internal void NotifyContextDeactivated(InputContextBase context)
//         {
//             _onContextDeactivated.OnNext(context);
//         }
//
//         // ==================== 配置 ====================
//
//         public void ApplyConfig(InputConfig config)
//         {
//             _config = config;
//             _logger.LogInformation($"[InputManager] 配置已应用: {config.Name}");
//         }
//
//         // ==================== 默认上下文 ====================
//
//         private void RegisterDefaultContexts()
//         {
//             _contextStack.RegisterContext<GlobalInputContext>(new GlobalInputContext());
//             _contextStack.RegisterContext<UIInputContext>(new UIInputContext());
//             _contextStack.RegisterContext<GameplayInputContext>(new GameplayInputContext());
//
//             _logger.LogDebug($"[InputManager] 默认输入上下文已注册: 3 个");
//         }
//     }
// }
