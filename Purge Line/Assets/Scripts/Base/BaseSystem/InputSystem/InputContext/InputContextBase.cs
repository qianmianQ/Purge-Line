// using System;
// using System.Collections.Generic;
// using System.Runtime.CompilerServices;
// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 输入上下文基类
//     /// 提供输入上下文的核心功能，包括输入动作管理、事件处理和状态控制
//     /// </summary>
//     public abstract class InputContextBase : IDisposable
//     {
//         // ==================== 数据结构 ====================
//
//         protected struct InputActionBinding
//         {
//             public InputAction Action;
//             public IDisposable Subscription;
//             public InputActionEvent Phase;
//         }
//
//         // ==================== 字段 ====================
//
//         private readonly string _name;
//         private readonly List<InputActionBinding> _bindings = new List<InputActionBinding>();
//         private readonly Dictionary<InputAction, List<IDisposable>> _actionSubscriptions = new Dictionary<InputAction, List<IDisposable>>();
//         private readonly Subject<Unit> _onActivated = new Subject<Unit>();
//         private readonly Subject<Unit> _onDeactivated = new Subject<Unit>();
//         private readonly CompositeDisposable _disposables = new CompositeDisposable();
//
//         private bool _isActive;
//         private bool _isDisposed;
//         private float _activationTime;
//
//         // ==================== 属性 ====================
//
//         public string Name => _name;
//         public bool IsActive => _isActive;
//         public float ActivationTime => _isActive ? Time.time - _activationTime : 0f;
//
//         public Observable<Unit> OnActivated => _onActivated.AsObservable();
//         public Observable<Unit> OnDeactivated => _onDeactivated.AsObservable();
//
//         // ==================== 构造函数 ====================
//
//         protected InputContextBase(string name)
//         {
//             _name = name;
//         }
//
//         // ==================== 生命周期方法 ====================
//
//         /// <summary>
//         /// 激活上下文（内部调用）
//         /// </summary>
//         internal void Activate()
//         {
//             if (_isDisposed) throw new ObjectDisposedException(_name);
//             if (_isActive) return;
//
//             _isActive = true;
//             _activationTime = Time.time;
//
//             OnContextActivated();
//             _onActivated.OnNext(Unit.Default);
//         }
//
//         /// <summary>
//         /// 停用上下文（内部调用）
//         /// </summary>
//         internal void Deactivate()
//         {
//             if (_isDisposed) throw new ObjectDisposedException(_name);
//             if (!_isActive) return;
//
//             _isActive = false;
//
//             OnContextDeactivated();
//             _onDeactivated.OnNext(Unit.Default);
//         }
//
//         /// <summary>
//         /// 更新上下文（内部调用）
//         /// </summary>
//         internal void Update(float deltaTime)
//         {
//             if (!_isActive || _isDisposed) return;
//
//             OnUpdate(deltaTime);
//         }
//
//         // ==================== 虚方法（子类覆盖） ====================
//
//         /// <summary>
//         /// 上下文激活时调用
//         /// </summary>
//         protected virtual void OnContextActivated()
//         {
//             // 启用所有绑定的输入动作
//             foreach (var binding in _bindings)
//             {
//                 binding.Action?.Enable();
//             }
//         }
//
//         /// <summary>
//         /// 上下文停用时调用
//         /// </summary>
//         protected virtual void OnContextDeactivated()
//         {
//             // 禁用所有绑定的输入动作
//             foreach (var binding in _bindings)
//             {
//                 binding.Action?.Disable();
//             }
//         }
//
//         /// <summary>
//         /// 每帧更新时调用
//         /// </summary>
//         protected virtual void OnUpdate(float deltaTime)
//         {
//         }
//
//         // ==================== 输入动作绑定 ====================
//
//         /// <summary>
//         /// 绑定输入动作到回调（Performed 阶段）
//         /// </summary>
//         protected void BindAction(InputAction action, Action<InputAction.CallbackContext> callback)
//         {
//             BindAction(action, InputActionEvent.Performed, callback);
//         }
//
//         /// <summary>
//         /// 绑定输入动作到回调（指定阶段）
//         /// </summary>
//         protected void BindAction(InputAction action, InputActionEvent phase, Action<InputAction.CallbackContext> callback)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(_name);
//             if (action == null) throw new ArgumentNullException(nameof(action));
//             if (callback == null) throw new ArgumentNullException(nameof(callback));
//
//             // 创建订阅
//             IDisposable subscription = null;
//
//             switch (phase)
//             {
//                 case InputActionEvent.Performed:
//                     subscription = action.performed += callback;
//                     break;
//                 case InputActionEvent.Started:
//                     subscription = action.started += callback;
//                     break;
//                 case InputActionEvent.Canceled:
//                     subscription = action.canceled += callback;
//                     break;
//             }
//
//             // 记录绑定
//             var binding = new InputActionBinding
//             {
//                 Action = action,
//                 Subscription = subscription,
//                 Phase = phase
//             };
//             _bindings.Add(binding);
//
//             // 如果上下文已激活，启用动作
//             if (_isActive)
//             {
//                 action.Enable();
//             }
//         }
//
//         /// <summary>
//         /// 绑定输入动作的 R3 Observable 流
//         /// </summary>
//         protected void BindActionObservable(InputAction action, Func<Observable<InputAction.CallbackContext>, Observable<Unit>> pipeline)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(_name);
//             if (action == null) throw new ArgumentNullException(nameof(action));
//             if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
//
//             var subject = new Subject<InputAction.CallbackContext>();
//             var performedHandle = action.performed += ctx => subject.OnNext(ctx);
//             var canceledHandle = action.canceled += ctx => subject.OnNext(ctx);
//
//             var disposable = pipeline(subject.AsObservable())
//                 .Subscribe()
//                 .AddTo(_disposables);
//
//             // 管理订阅
//             if (!_actionSubscriptions.TryGetValue(action, out var list))
//             {
//                 list = new List<IDisposable>();
//                 _actionSubscriptions[action] = list;
//             }
//             list.Add(Disposable.Create(() =>
//             {
//                 action.performed -= performedHandle;
//                 action.canceled -= canceledHandle;
//                 disposable.Dispose();
//                 subject.Dispose();
//             }));
//
//             if (_isActive)
//             {
//                 action.Enable();
//             }
//         }
//
//         /// <summary>
//         /// 解绑所有输入动作
//         /// </summary>
//         protected void UnbindAllActions()
//         {
//             // 清理标准绑定
//             foreach (var binding in _bindings)
//             {
//                 if (binding.Action != null)
//                 {
//                     switch (binding.Phase)
//                     {
//                         case InputActionEvent.Performed:
//                             binding.Action.performed -= binding.Subscription;
//                             break;
//                         case InputActionEvent.Started:
//                             binding.Action.started -= binding.Subscription;
//                             break;
//                         case InputActionEvent.Canceled:
//                             binding.Action.canceled -= binding.Subscription;
//                             break;
//                     }
//                     binding.Action.Disable();
//                 }
//             }
//             _bindings.Clear();
//
//             // 清理 R3 订阅
//             foreach (var kvp in _actionSubscriptions)
//             {
//                 foreach (var disposable in kvp.Value)
//                 {
//                     disposable.Dispose();
//                 }
//                 kvp.Key.Disable();
//             }
//             _actionSubscriptions.Clear();
//         }
//
//         // ==================== IDisposable ====================
//
//         public void Dispose()
//         {
//             if (_isDisposed) return;
//
//             if (_isActive)
//             {
//                 Deactivate();
//             }
//
//             UnbindAllActions();
//             _disposables.Dispose();
//             _onActivated.Dispose();
//             _onDeactivated.Dispose();
//
//             _isDisposed = true;
//         }
//
//         // ==================== 辅助方法 ====================
//
//         /// <summary>
//         /// 获取当前活动上下文的设备（如果有设备锁定）
//         /// </summary>
//         protected bool TryGetLockedDevice<T>(out T device) where T : InputDevice
//         {
//             device = null;
//             return false;
//         }
//
//         /// <summary>
//         /// 检查是否在指定时间内被调用（用于防抖）
//         /// </summary>
//         protected bool IsWithinCooldown(float cooldownSeconds, ref float lastInvokeTime)
//         {
//             if (Time.time - lastInvokeTime < cooldownSeconds)
//             {
//                 return true;
//             }
//             lastInvokeTime = Time.time;
//             return false;
//         }
//     }
//
//     /// <summary>
//     /// 输入动作事件阶段
//     /// </summary>
//     public enum InputActionEvent
//     {
//         Started,
//         Performed,
//         Canceled
//     }
// }
