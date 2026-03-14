// using System;
// using System.Collections.Generic;
// using System.Runtime.CompilerServices;
// using Microsoft.Extensions.Logging;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 输入上下文栈管理器
//     /// 实现输入优先级管理、上下文隔离和设备锁定
//     /// </summary>
//     public class InputContextStack : IDisposable
//     {
//         // ==================== 数据结构 ====================
//
//         private readonly struct StackEntry
//         {
//             public readonly InputContextBase Context;
//             public readonly int Priority;
//             public readonly bool IsModal;
//             public readonly List<InputDevice> LockedDevices;
//
//             public StackEntry(InputContextBase context, int priority, bool isModal)
//             {
//                 Context = context;
//                 Priority = priority;
//                 IsModal = isModal;
//                 LockedDevices = new List<InputDevice>();
//             }
//         }
//
//         // ==================== 字段 ====================
//
//         private readonly InputManager _manager;
//         private readonly List<StackEntry> _stack = new List<StackEntry>();
//         private readonly Dictionary<Type, InputContextBase> _registeredContexts = new Dictionary<Type, InputContextBase>();
//         private readonly Dictionary<InputDevice, InputContextBase> _deviceLocks = new Dictionary<InputDevice, InputContextBase>();
//         private readonly List<StackEntry> _pendingDeactivations = new List<StackEntry>();
//         private static ILogger _logger;
//
//         private bool _isDirty;
//         private bool _disposed;
//
//         // ==================== 属性 ====================
//
//         public InputContextBase CurrentContext => _stack.Count > 0 ? _stack[_stack.Count - 1].Context : null;
//         public int Count => _stack.Count;
//         public bool IsEmpty => _stack.Count == 0;
//
//         // ==================== 构造函数 ====================
//
//         public InputContextStack(InputManager manager)
//         {
//             _manager = manager;
//             _logger = GameLogger.Create("InputContextStack");
//         }
//
//         // ==================== 上下文注册 ====================
//
//         /// <summary>
//         /// 注册输入上下文类型
//         /// </summary>
//         public void RegisterContext<TContext>(TContext context) where TContext : InputContextBase
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (context == null) throw new ArgumentNullException(nameof(context));
//
//             var type = typeof(TContext);
//             _registeredContexts[type] = context;
//
//             _logger.LogDebug($"[InputContextStack] 上下文已注册: {type.Name}");
//         }
//
//         /// <summary>
//         /// 取消注册输入上下文类型
//         /// </summary>
//         public void UnregisterContext<TContext>() where TContext : InputContextBase
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//
//             var type = typeof(TContext);
//             _registeredContexts.Remove(type);
//
//             _logger.LogDebug($"[InputContextStack] 上下文已取消注册: {type.Name}");
//         }
//
//         // ==================== 入栈操作 ====================
//
//         /// <summary>
//         /// 推入上下文到栈顶
//         /// </summary>
//         public void PushContext<TContext>(bool isModal = false, int priority = 0) where TContext : InputContextBase
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//
//             var type = typeof(TContext);
//             if (!_registeredContexts.TryGetValue(type, out var context))
//             {
//                 throw new InvalidOperationException($"[InputContextStack] 上下文未注册: {type.Name}");
//             }
//
//             PushContextInternal(context, isModal, priority);
//         }
//
//         /// <summary>
//         /// 推入上下文实例到栈顶
//         /// </summary>
//         public void PushContext(InputContextBase context, bool isModal = false, int priority = 0)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (context == null) throw new ArgumentNullException(nameof(context));
//
//             PushContextInternal(context, isModal, priority);
//         }
//
//         private void PushContextInternal(InputContextBase context, bool isModal, int priority)
//         {
//             // 检查是否已经在栈中
//             var existingIndex = FindEntryIndex(context);
//             if (existingIndex >= 0)
//             {
//                 _logger.LogWarning($"[InputContextStack] 上下文已在栈中，移至栈顶: {context.Name}");
//                 RemoveEntryAt(existingIndex);
//             }
//
//             var entry = new StackEntry(context, priority, isModal);
//             _stack.Add(entry);
//
//             // 激活新上下文
//             ActivateContext(context);
//
//             _isDirty = true;
//             _logger.LogDebug($"[InputContextStack] 推入上下文: {context.Name} (Modal: {isModal}, Priority: {priority})");
//         }
//
//         // ==================== 出栈操作 ====================
//
//         /// <summary>
//         /// 弹出栈顶上下文
//         /// </summary>
//         public InputContextBase PopContext()
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (_stack.Count == 0) return null;
//
//             var index = _stack.Count - 1;
//             var entry = _stack[index];
//             RemoveEntryAt(index);
//
//             _isDirty = true;
//             _logger.LogDebug($"[InputContextStack] 弹出上下文: {entry.Context.Name}");
//
//             return entry.Context;
//         }
//
//         /// <summary>
//         /// 弹出指定类型的上下文
//         /// </summary>
//         public bool PopContext<TContext>() where TContext : InputContextBase
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//
//             var type = typeof(TContext);
//             for (int i = _stack.Count - 1; i >= 0; i--)
//             {
//                 if (_stack[i].Context.GetType() == type)
//                 {
//                     RemoveEntryAt(i);
//                     _isDirty = true;
//                     _logger.LogDebug($"[InputContextStack] 弹出上下文: {type.Name}");
//                     return true;
//                 }
//             }
//
//             return false;
//         }
//
//         /// <summary>
//         /// 清空栈到指定上下文
//         /// </summary>
//         public void PopToContext<TContext>() where TContext : InputContextBase
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//
//             var type = typeof(TContext);
//             for (int i = _stack.Count - 1; i >= 0; i--)
//             {
//                 if (_stack[i].Context.GetType() == type)
//                 {
//                     // 移除后面的所有上下文
//                     while (_stack.Count > i + 1)
//                     {
//                         RemoveEntryAt(_stack.Count - 1);
//                     }
//                     _isDirty = true;
//                     _logger.LogDebug($"[InputContextStack] 弹出到上下文: {type.Name}");
//                     return;
//                 }
//             }
//
//             _logger.LogWarning($"[InputContextStack] 未找到上下文: {type.Name}");
//         }
//
//         /// <summary>
//         /// 清空整个栈
//         /// </summary>
//         public void Clear()
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//
//             while (_stack.Count > 0)
//             {
//                 RemoveEntryAt(_stack.Count - 1);
//             }
//
//             _isDirty = true;
//             _logger.LogDebug("[InputContextStack] 上下文栈已清空");
//         }
//
//         // ==================== 查询操作 ====================
//
//         /// <summary>
//         /// 检查是否包含指定类型的上下文
//         /// </summary>
//         public bool Contains<TContext>() where TContext : InputContextBase
//         {
//             return FindEntryIndex(typeof(TContext)) >= 0;
//         }
//
//         /// <summary>
//         /// 检查是否包含指定上下文实例
//         /// </summary>
//         public bool Contains(InputContextBase context)
//         {
//             return FindEntryIndex(context) >= 0;
//         }
//
//         /// <summary>
//         /// 获取指定类型的上下文（如果在栈中）
//         /// </summary>
//         public TContext GetContext<TContext>() where TContext : InputContextBase
//         {
//             var index = FindEntryIndex(typeof(TContext));
//             return index >= 0 ? _stack[index].Context as TContext : null;
//         }
//
//         /// <summary>
//         /// 尝试获取栈顶上下文
//         /// </summary>
//         public bool TryPeek(out InputContextBase context)
//         {
//             if (_stack.Count > 0)
//             {
//                 context = _stack[_stack.Count - 1].Context;
//                 return true;
//             }
//
//             context = null;
//             return false;
//         }
//
//         // ==================== 设备锁定 ====================
//
//         /// <summary>
//         /// 锁定设备到当前上下文
//         /// </summary>
//         public void LockDevice(InputDevice device)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//             if (!TryPeek(out var currentContext))
//             {
//                 _logger.LogWarning("[InputContextStack] 栈为空，无法锁定设备");
//                 return;
//             }
//
//             LockDevice(device, currentContext);
//         }
//
//         /// <summary>
//         /// 锁定设备到指定上下文
//         /// </summary>
//         public void LockDevice(InputDevice device, InputContextBase context)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//             if (context == null) throw new ArgumentNullException(nameof(context));
//
//             // 移除现有锁定
//             if (_deviceLocks.TryGetValue(device, out var existingContext))
//             {
//                 UnlockDeviceInternal(device, existingContext);
//             }
//
//             // 添加新锁定
//             _deviceLocks[device] = context;
//
//             // 在栈条目上记录锁定
//             var entryIndex = FindEntryIndex(context);
//             if (entryIndex >= 0)
//             {
//                 ref var entry = ref _stack.AsSpan()[entryIndex];
//                 entry.LockedDevices.Add(device);
//             }
//
//             _logger.LogDebug($"[InputContextStack] 设备已锁定: {device.name} -> {context.Name}");
//         }
//
//         /// <summary>
//         /// 解锁设备
//         /// </summary>
//         public void UnlockDevice(InputDevice device)
//         {
//             if (_disposed) throw new ObjectDisposedException(nameof(InputContextStack));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             if (_deviceLocks.TryGetValue(device, out var context))
//             {
//                 UnlockDeviceInternal(device, context);
//                 _logger.LogDebug($"[InputContextStack] 设备已解锁: {device.name}");
//             }
//         }
//
//         /// <summary>
//         /// 检查设备是否被锁定到指定上下文
//         /// </summary>
//         public bool IsDeviceLockedTo(InputDevice device, InputContextBase context)
//         {
//             if (_deviceLocks.TryGetValue(device, out var lockedContext))
//             {
//                 return lockedContext == context;
//             }
//             return false;
//         }
//
//         /// <summary>
//         /// 获取设备当前锁定的上下文
//         /// </summary>
//         public InputContextBase GetDeviceLock(InputDevice device)
//         {
//             _deviceLocks.TryGetValue(device, out var context);
//             return context;
//         }
//
//         /// <summary>
//         /// 检查设备是否应该向当前上下文发送输入
//         /// </summary>
//         public bool ShouldReceiveInput(InputDevice device, InputContextBase context)
//         {
//             if (_deviceLocks.TryGetValue(device, out var lockedContext))
//             {
//                 return lockedContext == context;
//             }
//
//             // 无锁定时，检查是否是栈顶上下文
//             if (TryPeek(out var topContext))
//             {
//                 return context == topContext;
//             }
//
//             return false;
//         }
//
//         // ==================== 私有方法 ====================
//
//         private void RemoveEntryAt(int index)
//         {
//             var entry = _stack[index];
//
//             // 解锁所有关联设备
//             foreach (var device in entry.LockedDevices)
//             {
//                 _deviceLocks.Remove(device);
//             }
//
//             // 停用上下文
//             DeactivateContext(entry.Context);
//
//             _stack.RemoveAt(index);
//
//             // 如果移除了栈顶，激活新的栈顶
//             if (index == _stack.Count && _stack.Count > 0)
//             {
//                 ActivateContext(_stack[_stack.Count - 1].Context);
//             }
//         }
//
//         private void ActivateContext(InputContextBase context)
//         {
//             if (!context.IsActive)
//             {
//                 context.Activate();
//                 _manager.NotifyContextActivated(context);
//                 _logger.LogDebug($"[InputContextStack] 上下文已激活: {context.Name}");
//             }
//         }
//
//         private void DeactivateContext(InputContextBase context)
//         {
//             if (context.IsActive)
//             {
//                 context.Deactivate();
//                 _manager.NotifyContextDeactivated(context);
//                 _logger.LogDebug($"[InputContextStack] 上下文已停用: {context.Name}");
//             }
//         }
//
//         private void UnlockDeviceInternal(InputDevice device, InputContextBase context)
//         {
//             _deviceLocks.Remove(device);
//
//             // 从栈条目移除设备
//             var entryIndex = FindEntryIndex(context);
//             if (entryIndex >= 0)
//             {
//                 ref var entry = ref _stack.AsSpan()[entryIndex];
//                 entry.LockedDevices.Remove(device);
//             }
//         }
//
//         private int FindEntryIndex(Type contextType)
//         {
//             for (int i = _stack.Count - 1; i >= 0; i--)
//             {
//                 if (_stack[i].Context.GetType() == contextType)
//                 {
//                     return i;
//                 }
//             }
//             return -1;
//         }
//
//         private int FindEntryIndex(InputContextBase context)
//         {
//             for (int i = _stack.Count - 1; i >= 0; i--)
//             {
//                 if (_stack[i].Context == context)
//                 {
//                     return i;
//                 }
//             }
//             return -1;
//         }
//
//         // ==================== 更新 ====================
//
//         public void Update(float deltaTime)
//         {
//             if (_disposed) return;
//
//             // 更新活动上下文
//             for (int i = _stack.Count - 1; i >= 0; i--)
//             {
//                 var context = _stack[i].Context;
//                 if (context.IsActive)
//                 {
//                     context.Update(deltaTime);
//                 }
//             }
//         }
//
//         // ==================== IDisposable ====================
//
//         public void Dispose()
//         {
//             if (_disposed) return;
//
//             Clear();
//             _registeredContexts.Clear();
//             _deviceLocks.Clear();
//             _disposed = true;
//         }
//     }
// }
