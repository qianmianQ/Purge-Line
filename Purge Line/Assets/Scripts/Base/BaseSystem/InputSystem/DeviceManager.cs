// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Microsoft.Extensions.Logging;
// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.InputSystem.Utilities;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 设备管理器
//     /// 负责设备检测、热插拔、设备锁定和智能重连
//     /// </summary>
//     public class DeviceManager : IDisposable
//     {
//         // ==================== 数据结构 ====================
//
//         private class DeviceState
//         {
//             public InputDevice Device;
//             public bool IsEnabled;
//             public bool IsLocked;
//             public int PlayerIndex;
//             public float LastActiveTime;
//             public float ConnectionTime;
//             public IDisposable PollingSubscription;
//         }
//
//         // ==================== 字段 ====================
//
//         private readonly InputManager _manager;
//         private readonly Dictionary<InputDevice, DeviceState> _deviceStates = new Dictionary<InputDevice, DeviceState>();
//         private readonly Dictionary<int, List<InputDevice>> _playerDevices = new Dictionary<int, List<InputDevice>>();
//         private readonly Subject<DeviceConnectionEvent> _onDeviceConnected = new Subject<DeviceConnectionEvent>();
//         private readonly Subject<DeviceConnectionEvent> _onDeviceDisconnected = new Subject<DeviceConnectionEvent>();
//         private readonly Subject<DeviceStateChangedEvent> _onDeviceStateChanged = new Subject<DeviceStateChangedEvent>();
//         private static ILogger _logger;
//
//         private readonly List<InputDevice> _pendingRemovals = new List<InputDevice>();
//         private DeviceFilter _filter;
//         private bool _disposed;
//
//         // ==================== 属性 ====================
//
//         public IReadOnlyCollection<InputDevice> AllDevices => _deviceStates.Keys;
//         public IReadOnlyCollection<InputDevice> EnabledDevices => _deviceStates.Where(kv => kv.Value.IsEnabled).Select(kv => kv.Key).ToList();
//         public IReadOnlyCollection<InputDevice> DisabledDevices => _deviceStates.Where(kv => !kv.Value.IsEnabled).Select(kv => kv.Key).ToList();
//
//         public Observable<DeviceConnectionEvent> OnDeviceConnected => _onDeviceConnected.AsObservable();
//         public Observable<DeviceConnectionEvent> OnDeviceDisconnected => _onDeviceDisconnected.AsObservable();
//         public Observable<DeviceStateChangedEvent> OnDeviceStateChanged => _onDeviceStateChanged.AsObservable();
//
//         // ==================== 构造函数 ====================
//
//         public DeviceManager(InputManager manager)
//         {
//             _manager = manager;
//             _logger = GameLogger.Create("DeviceManager");
//         }
//
//         // ==================== 设备发现和刷新 ====================
//
//         /// <summary>
//         /// 刷新所有设备
//         /// </summary>
//         public void RefreshDevices()
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//
//             _logger.LogDebug("[DeviceManager] 刷新设备列表");
//
//             // 添加所有当前连接的设备
//             foreach (var device in InputSystem.devices)
//             {
//                 AddDevice(device);
//             }
//         }
//
//         /// <summary>
//         /// 添加设备（内部调用）
//         /// </summary>
//         internal void AddDevice(InputDevice device)
//         {
//             if (_disposed) return;
//             if (device == null) return;
//
//             // 检查设备过滤器
//             if (_filter != null && !_filter.ShouldAcceptDevice(device))
//             {
//                 _logger.LogDebug($"[DeviceManager] 设备被过滤: {device.name}");
//                 return;
//             }
//
//             // 检查是否已存在
//             if (_deviceStates.ContainsKey(device))
//             {
//                 return;
//             }
//
//             // 创建设备状态
//             var state = new DeviceState
//             {
//                 Device = device,
//                 IsEnabled = true,
//                 IsLocked = false,
//                 PlayerIndex = -1,
//                 LastActiveTime = Time.realtimeSinceStartup,
//                 ConnectionTime = Time.realtimeSinceStartup
//             };
//
//             _deviceStates[device] = state;
//
//             // 开始监听设备活动
//             StartDevicePolling(device, state);
//
//             // 发送连接事件
//             var evt = new DeviceConnectionEvent
//             {
//                 Device = device,
//                 DeviceType = GetDeviceType(device),
//                 DeviceName = device.name,
//                 ConnectionTime = state.ConnectionTime
//             };
//             _onDeviceConnected.OnNext(evt);
//
//             _logger.LogDebug($"[DeviceManager] 设备已添加: {device.name} ({GetDeviceType(device)})");
//         }
//
//         /// <summary>
//         /// 移除设备（内部调用）
//         /// </summary>
//         internal void RemoveDevice(InputDevice device)
//         {
//             if (_disposed) return;
//             if (device == null) return;
//
//             if (_deviceStates.TryGetValue(device, out var state))
//             {
//                 // 停止轮询
//                 state.PollingSubscription?.Dispose();
//
//                 // 从玩家设备中移除
//                 if (state.PlayerIndex >= 0 && _playerDevices.TryGetValue(state.PlayerIndex, out var list))
//                 {
//                     list.Remove(device);
//                 }
//
//                 // 移除状态
//                 _deviceStates.Remove(device);
//
//                 // 发送断开事件
//                 var evt = new DeviceConnectionEvent
//                 {
//                     Device = device,
//                     DeviceType = GetDeviceType(device),
//                     DeviceName = device.name,
//                     ConnectionTime = state.ConnectionTime
//                 };
//                 _onDeviceDisconnected.OnNext(evt);
//
//                 _logger.LogDebug($"[DeviceManager] 设备已移除: {device.name}");
//             }
//         }
//
//         /// <summary>
//         /// 设置设备启用状态（内部调用）
//         /// </summary>
//         internal void SetDeviceEnabled(InputDevice device, bool enabled)
//         {
//             if (_disposed) return;
//             if (device == null) return;
//
//             if (_deviceStates.TryGetValue(device, out var state) && state.IsEnabled != enabled)
//             {
//                 state.IsEnabled = enabled;
//
//                 var evt = new DeviceStateChangedEvent
//                 {
//                     Device = device,
//                     OldState = !enabled,
//                     NewState = enabled,
//                     ChangeType = DeviceStateChangeType.Enabled
//                 };
//                 _onDeviceStateChanged.OnNext(evt);
//
//                 _logger.LogDebug($"[DeviceManager] 设备状态变化: {device.name} -> {(enabled ? "启用" : "禁用")}");
//             }
//         }
//
//         // ==================== 设备启用/禁用 ====================
//
//         /// <summary>
//         /// 启用设备
//         /// </summary>
//         public void EnableDevice(InputDevice device)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             InputSystem.EnableDevice(device);
//         }
//
//         /// <summary>
//         /// 禁用设备
//         /// </summary>
//         public void DisableDevice(InputDevice device)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             InputSystem.DisableDevice(device);
//         }
//
//         /// <summary>
//         /// 启用所有设备
//         /// </summary>
//         public void EnableAllDevices()
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//
//             foreach (var device in InputSystem.devices)
//             {
//                 InputSystem.EnableDevice(device);
//             }
//
//             _logger.LogDebug("[DeviceManager] 已启用所有设备");
//         }
//
//         /// <summary>
//         /// 禁用所有设备
//         /// </summary>
//         public void DisableAllDevices()
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//
//             foreach (var device in InputSystem.devices)
//             {
//                 InputSystem.DisableDevice(device);
//             }
//
//             _logger.LogDebug("[DeviceManager] 已禁用所有设备");
//         }
//
//         // ==================== 设备过滤 ====================
//
//         /// <summary>
//         /// 设置设备过滤器
//         /// </summary>
//         public void SetFilter(DeviceFilter filter)
//         {
//             _filter = filter;
//             _logger.LogDebug($"[DeviceManager] 过滤器已设置: {filter?.Name ?? "None"}");
//         }
//
//         /// <summary>
//         /// 清除设备过滤器
//         /// </summary>
//         public void ClearFilter()
//         {
//             _filter = null;
//             _logger.LogDebug("[DeviceManager] 过滤器已清除");
//         }
//
//         // ==================== 玩家设备分配 ====================
//
//         /// <summary>
//         /// 分配设备给玩家
//         /// </summary>
//         public void AssignDeviceToPlayer(InputDevice device, int playerIndex)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//             if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
//
//             if (_deviceStates.TryGetValue(device, out var state))
//             {
//                 // 从旧玩家移除
//                 if (state.PlayerIndex >= 0 && _playerDevices.TryGetValue(state.PlayerIndex, out var oldList))
//                 {
//                     oldList.Remove(device);
//                 }
//
//                 // 添加到新玩家
//                 state.PlayerIndex = playerIndex;
//                 if (!_playerDevices.TryGetValue(playerIndex, out var newList))
//                 {
//                     newList = new List<InputDevice>();
//                     _playerDevices[playerIndex] = newList;
//                 }
//                 newList.Add(device);
//
//                 _logger.LogDebug($"[DeviceManager] 设备已分配给玩家 {playerIndex}: {device.name}");
//             }
//         }
//
//         /// <summary>
//         /// 取消设备的玩家分配
//         /// </summary>
//         public void UnassignDeviceFromPlayer(InputDevice device)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             if (_deviceStates.TryGetValue(device, out var state) && state.PlayerIndex >= 0)
//             {
//                 if (_playerDevices.TryGetValue(state.PlayerIndex, out var list))
//                 {
//                     list.Remove(device);
//                 }
//                 state.PlayerIndex = -1;
//
//                 _logger.LogDebug($"[DeviceManager] 设备已取消分配: {device.name}");
//             }
//         }
//
//         /// <summary>
//         /// 获取玩家的所有设备
//         /// </summary>
//         public IReadOnlyList<InputDevice> GetPlayerDevices(int playerIndex)
//         {
//             if (_playerDevices.TryGetValue(playerIndex, out var list))
//             {
//                 return list.AsReadOnly();
//             }
//             return Array.Empty<InputDevice>();
//         }
//
//         /// <summary>
//         /// 自动分配设备给玩家
//         /// </summary>
//         public void AutoAssignDevices(int maxPlayers = 4)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(DeviceManager));
//
//             int playerIndex = 0;
//
//             // 先处理游戏手柄
//             foreach (var gamepad in Gamepad.all)
//             {
//                 if (playerIndex < maxPlayers)
//                 {
//                     AssignDeviceToPlayer(gamepad, playerIndex++);
//                 }
//             }
//
//             // 然后处理键盘/鼠标（可能需要分屏）
//             // 对于键盘/鼠标，通常需要用户交互来分配
//         }
//
//         // ==================== 设备查询 ====================
//
//         /// <summary>
//         /// 获取指定类型的设备
//         /// </summary>
//         public IEnumerable<T> GetDevices<T>() where T : InputDevice
//         {
//             return InputSystem.devices.OfType<T>();
//         }
//
//         /// <summary>
//         /// 获取指定类型的第一个设备
//         /// </summary>
//         public T GetDevice<T>() where T : InputDevice
//         {
//             return InputSystem.GetDevice<T>();
//         }
//
//         /// <summary>
//         /// 检查设备是否连接
//         /// </summary>
//         public bool IsDeviceConnected(InputDevice device)
//         {
//             return device != null && _deviceStates.ContainsKey(device);
//         }
//
//         /// <summary>
//         /// 检查设备是否启用
//         /// </summary>
//         public bool IsDeviceEnabled(InputDevice device)
//         {
//             if (_deviceStates.TryGetValue(device, out var state))
//             {
//                 return state.IsEnabled;
//             }
//             return false;
//         }
//
//         /// <summary>
//         /// 获取设备类型
//         /// </summary>
//         public static InputDeviceType GetDeviceType(InputDevice device)
//         {
//             if (device is Gamepad) return InputDeviceType.Gamepad;
//             if (device is Keyboard) return InputDeviceType.Keyboard;
//             if (device is Mouse) return InputDeviceType.Mouse;
//             if (device is Touchscreen) return InputDeviceType.Touchscreen;
//             if (device is Joystick) return InputDeviceType.Joystick;
//             if (device is Pointer) return InputDeviceType.Pointer;
//             if (device is Pen) return InputDeviceType.Pen;
//             return InputDeviceType.Unknown;
//         }
//
//         // ==================== 私有方法 ====================
//
//         private void StartDevicePolling(InputDevice device, DeviceState state)
//         {
//             // 使用 R3 Observable 监听设备活动
//             var observable = Observable.EveryUpdate()
//                 .Where(_ => !_disposed && device != null && device.enabled)
//                 .Subscribe(_ =>
//                 {
//                     if (device.wasUpdatedThisFrame)
//                     {
//                         state.LastActiveTime = Time.realtimeSinceStartup;
//                     }
//                 });
//
//             state.PollingSubscription = observable;
//         }
//
//         // ==================== 更新 ====================
//
//         public void Update()
//         {
//             if (_disposed) return;
//
//             // 处理待移除的设备
//             foreach (var device in _pendingRemovals)
//             {
//                 RemoveDevice(device);
//             }
//             _pendingRemovals.Clear();
//         }
//
//         // ==================== IDisposable ====================
//
//         public void Dispose()
//         {
//             if (_disposed) return;
//
//             // 清理所有设备状态
//             foreach (var state in _deviceStates.Values)
//             {
//                 state.PollingSubscription?.Dispose();
//             }
//             _deviceStates.Clear();
//             _playerDevices.Clear();
//
//             _onDeviceConnected.Dispose();
//             _onDeviceDisconnected.Dispose();
//             _onDeviceStateChanged.Dispose();
//
//             _disposed = true;
//         }
//     }
//
//     // ====================================================================
//     // 设备事件定义
//     // ====================================================================
//
//     /// <summary>
//     /// 设备连接事件
//     /// </summary>
//     public struct DeviceConnectionEvent
//     {
//         public InputDevice Device;
//         public InputDeviceType DeviceType;
//         public string DeviceName;
//         public float ConnectionTime;
//     }
//
//     /// <summary>
//     /// 设备状态变化事件
//     /// </summary>
//     public struct DeviceStateChangedEvent
//     {
//         public InputDevice Device;
//         public bool OldState;
//         public bool NewState;
//         public DeviceStateChangeType ChangeType;
//     }
//
//     /// <summary>
//     /// 设备状态变化类型
//     /// </summary>
//     public enum DeviceStateChangeType
//     {
//         Enabled,
//         Locked,
//         Assigned
//     }
//
//     /// <summary>
//     /// 设备类型
//     /// </summary>
//     public enum InputDeviceType
//     {
//         Unknown,
//         Gamepad,
//         Keyboard,
//         Mouse,
//         Touchscreen,
//         Joystick,
//         Pointer,
//         Pen
//     }
//
//     // ====================================================================
//     // 设备过滤器
//     // ====================================================================
//
//     /// <summary>
//     /// 设备过滤器基类
//     /// </summary>
//     public abstract class DeviceFilter
//     {
//         public abstract string Name { get; }
//         public abstract bool ShouldAcceptDevice(InputDevice device);
//     }
//
//     /// <summary>
//     /// 基于类型的设备过滤器
//     /// </summary>
//     public class TypeDeviceFilter : DeviceFilter
//     {
//         private readonly HashSet<InputDeviceType> _allowedTypes;
//
//         public override string Name => "TypeFilter";
//
//         public TypeDeviceFilter(params InputDeviceType[] types)
//         {
//             _allowedTypes = new HashSet<InputDeviceType>(types);
//         }
//
//         public override bool ShouldAcceptDevice(InputDevice device)
//         {
//             var type = DeviceManager.GetDeviceType(device);
//             return _allowedTypes.Contains(type);
//         }
//     }
//
//     /// <summary>
//     /// 组合设备过滤器
//     /// </summary>
//     public class CompositeDeviceFilter : DeviceFilter
//     {
//         private readonly List<DeviceFilter> _filters;
//         private readonly bool _requireAll;
//
//         public override string Name => $"CompositeFilter({_filters.Count})";
//
//         public CompositeDeviceFilter(bool requireAll = true)
//         {
//             _filters = new List<DeviceFilter>();
//             _requireAll = requireAll;
//         }
//
//         public void AddFilter(DeviceFilter filter)
//         {
//             _filters.Add(filter);
//         }
//
//         public override bool ShouldAcceptDevice(InputDevice device)
//         {
//             if (_filters.Count == 0) return true;
//
//             if (_requireAll)
//             {
//                 foreach (var filter in _filters)
//                 {
//                     if (!filter.ShouldAcceptDevice(device))
//                         return false;
//                 }
//                 return true;
//             }
//             else
//             {
//                 foreach (var filter in _filters)
//                 {
//                     if (filter.ShouldAcceptDevice(device))
//                         return true;
//                 }
//                 return false;
//             }
//         }
//     }
// }
