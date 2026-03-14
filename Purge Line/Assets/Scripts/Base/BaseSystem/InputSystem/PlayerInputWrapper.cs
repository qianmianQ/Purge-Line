// using System;
// using System.Collections.Generic;
// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 玩家输入包装器
//     /// 提供多玩家输入的独立实例和管理
//     /// </summary>
//     public class PlayerInputWrapper : IDisposable
//     {
//         // ==================== 字段 ====================
//
//         private readonly int _playerIndex;
//         private readonly InputActionMap _map;
//         private readonly List<InputActionWrapper> _actions = new List<InputActionWrapper>();
//         private readonly List<InputGesture> _gestures = new List<InputGesture>();
//         private readonly Dictionary<InputDevice, object> _deviceBindings = new Dictionary<InputDevice, object>();
//         private readonly Subject<InputAction.CallbackContext> _onAnyAction = new Subject<InputAction.CallbackContext>();
//         private readonly Subject<GestureResult> _onAnyGesture = new Subject<GestureResult>();
//         private readonly CompositeDisposable _disposables = new CompositeDisposable();
//
//         private bool _isEnabled;
//         private bool _isDisposed;
//
//         // ==================== 属性 ====================
//
//         public int PlayerIndex => _playerIndex;
//         public bool IsEnabled => _isEnabled;
//         public InputActionMap ActionMap => _map;
//         public IReadOnlyList<InputActionWrapper> Actions => _actions.AsReadOnly();
//         public IReadOnlyList<InputGesture> Gestures => _gestures.AsReadOnly();
//
//         public IObservable<InputAction.CallbackContext> OnAnyAction => _onAnyAction.AsObservable();
//         public IObservable<GestureResult> OnAnyGesture => _onAnyGesture.AsObservable();
//
//         // ==================== 构造函数 ====================
//
//         public PlayerInputWrapper(int playerIndex)
//         {
//             _playerIndex = playerIndex;
//             _map = new InputActionMap($"Player{playerIndex}");
//
//             // 创建默认的玩家输入动作
//             CreateDefaultActions();
//
//             // 创建默认手势
//             CreateDefaultGestures();
//
//             _logger = GameLogger.Create($"Player{playerIndex}Input");
//         }
//
//         // ==================== 动作创建 ====================
//
//         private void CreateDefaultActions()
//         {
//             // 移动
//             var moveAction = _map.AddAction("Move", type: InputActionType.Value, binding: "<Gamepad>/leftStick");
//             moveAction.AddBinding("<Keyboard>/w");
//             moveAction.AddBinding("<Keyboard>/a");
//             moveAction.AddBinding("<Keyboard>/s");
//             moveAction.AddBinding("<Keyboard>/d");
//             AddAction(new InputActionWrapper("Move", moveAction));
//
//             // 视角
//             var lookAction = _map.AddAction("Look", type: InputActionType.Value, binding: "<Gamepad>/rightStick");
//             lookAction.AddBinding("<Mouse>/delta");
//             AddAction(new InputActionWrapper("Look", lookAction));
//
//             // 攻击
//             var attackAction = _map.AddAction("Attack", type: InputActionType.Button, binding: "<Gamepad>/rightTrigger");
//             attackAction.AddBinding("<Mouse>/leftButton");
//             AddAction(new InputActionWrapper("Attack", attackAction));
//
//             // 技能
//             var skillAction = _map.AddAction("Skill", type: InputActionType.Button, binding: "<Gamepad>/leftTrigger");
//             skillAction.AddBinding("<Mouse>/rightButton");
//             AddAction(new InputActionWrapper("Skill", skillAction));
//
//             // 跳跃/交互
//             var jumpAction = _map.AddAction("Jump", type: InputActionType.Button, binding: "<Gamepad>/buttonSouth");
//             jumpAction.AddBinding("<Keyboard>/space");
//             AddAction(new InputActionWrapper("Jump", jumpAction));
//
//             // 暂停
//             var pauseAction = _map.AddAction("Pause", type: InputActionType.Button, binding: "<Gamepad>/start");
//             pauseAction.AddBinding("<Keyboard>/escape");
//             AddAction(new InputActionWrapper("Pause", pauseAction));
//         }
//
//         private void CreateDefaultGestures()
//         {
//             // 双击
//             AddGesture(GestureRecognizer.Create(GestureType.DoubleClick));
//
//             // 长按
//             AddGesture(GestureRecognizer.Create(GestureType.LongPress));
//
//             // 拖拽
//             AddGesture(GestureRecognizer.Create(GestureType.Drag));
//         }
//
//         // ==================== 动作管理 ====================
//
//         public void AddAction(InputActionWrapper actionWrapper)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (actionWrapper == null) throw new ArgumentNullException(nameof(actionWrapper));
//
//             _actions.Add(actionWrapper);
//
//             // 监听动作事件
//             var subscription = actionWrapper.OnPerformed
//                 .Subscribe(ctx =>
//                 {
//                     _onAnyAction.OnNext(ctx);
//                     ProcessGestures(ctx);
//                 })
//                 .AddTo(_disposables);
//
//             // 启用动作
//             if (_isEnabled)
//             {
//                 actionWrapper.Enable();
//             }
//         }
//
//         public void RemoveAction(InputActionWrapper actionWrapper)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (actionWrapper == null) throw new ArgumentNullException(nameof(actionWrapper));
//
//             if (_actions.Remove(actionWrapper))
//             {
//                 actionWrapper.Disable();
//             }
//         }
//
//         public InputActionWrapper GetAction(string name)
//         {
//             return _actions.Find(x => x.Name == name);
//         }
//
//         // ==================== 手势管理 ====================
//
//         public void AddGesture(InputGesture gesture)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (gesture == null) throw new ArgumentNullException(nameof(gesture));
//
//             _gestures.Add(gesture);
//
//             var subscription = gesture.OnGestureRecognized
//                 .Subscribe(result => _onAnyGesture.OnNext(result))
//                 .AddTo(_disposables);
//         }
//
//         public void RemoveGesture(InputGesture gesture)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (gesture == null) throw new ArgumentNullException(nameof(gesture));
//
//             if (_gestures.Remove(gesture))
//             {
//                 gesture.Dispose();
//             }
//         }
//
//         public InputGesture GetGesture(GestureType type)
//         {
//             return _gestures.Find(x => x.Name == type.ToString());
//         }
//
//         // ==================== 设备绑定 ====================
//
//         public void BindDevice(InputDevice device)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             if (!_deviceBindings.ContainsKey(device))
//             {
//                 // 创建设备绑定
//                 var binding = new PlayerDeviceBinding(device, this);
//                 _deviceBindings[device] = binding;
//
//                 // 启用设备过滤
//                 foreach (var action in _actions)
//                 {
//                     action.Action.AddBindingGroup(device);
//                 }
//
//                 _logger.LogDebug($"[Player{_playerIndex}] 设备已绑定: {device.name}");
//             }
//         }
//
//         public void UnbindDevice(InputDevice device)
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (device == null) throw new ArgumentNullException(nameof(device));
//
//             if (_deviceBindings.TryGetValue(device, out var binding))
//             {
//                 (_deviceBindings[device] as IDisposable)?.Dispose();
//                 _deviceBindings.Remove(device);
//
//                 foreach (var action in _actions)
//                 {
//                     action.Action.RemoveBindingGroup(device);
//                 }
//
//                 _logger.LogDebug($"[Player{_playerIndex}] 设备已解绑: {device.name}");
//             }
//         }
//
//         public bool IsDeviceBound(InputDevice device)
//         {
//             return _deviceBindings.ContainsKey(device);
//         }
//
//         public IReadOnlyList<InputDevice> BoundDevices => _deviceBindings.Keys.ToList();
//
//         // ==================== 启用/禁用 ====================
//
//         public void Enable()
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (_isEnabled) return;
//
//             _map.Enable();
//
//             foreach (var action in _actions)
//             {
//                 action.Enable();
//             }
//
//             _isEnabled = true;
//             _logger.LogDebug($"[Player{_playerIndex}] 输入已启用");
//         }
//
//         public void Disable()
//         {
//             if (_isDisposed) throw new ObjectDisposedException(nameof(PlayerInputWrapper));
//             if (!_isEnabled) return;
//
//             _map.Disable();
//
//             foreach (var action in _actions)
//             {
//                 action.Disable();
//             }
//
//             _isEnabled = false;
//             _logger.LogDebug($"[Player{_playerIndex}] 输入已禁用");
//         }
//
//         // ==================== 手势处理 ====================
//
//         private void ProcessGestures(InputAction.CallbackContext context)
//         {
//             foreach (var gesture in _gestures)
//             {
//                 gesture.ProcessInput(context);
//             }
//         }
//
//         // ==================== 更新 ====================
//
//         public void Update(float deltaTime)
//         {
//             if (_isDisposed) return;
//
//             // 更新手势
//             foreach (var gesture in _gestures)
//             {
//                 gesture.Update(deltaTime);
//             }
//
//             // 清理完成的手势（可选）
//             for (int i = _gestures.Count - 1; i >= 0; i--)
//             {
//                 if (_gestures[i].IsCompleted)
//                 {
//                     _gestures[i].Reset();
//                 }
//             }
//         }
//
//         // ==================== IDisposable ====================
//
//         public void Dispose()
//         {
//             if (_isDisposed) return;
//
//             Disable();
//
//             foreach (var binding in _deviceBindings.Values)
//             {
//                 (binding as IDisposable)?.Dispose();
//             }
//
//             foreach (var gesture in _gestures)
//             {
//                 gesture.Dispose();
//             }
//
//             foreach (var action in _actions)
//             {
//                 action.Dispose();
//             }
//
//             _disposables.Dispose();
//             _onAnyAction.Dispose();
//             _onAnyGesture.Dispose();
//             _map.Dispose();
//
//             _deviceBindings.Clear();
//             _gestures.Clear();
//             _actions.Clear();
//
//             _isDisposed = true;
//         }
//     }
//
//     // ====================================================================
//     // 输入动作包装器
//     // ====================================================================
//
//     public class InputActionWrapper : IDisposable
//     {
//         public string Name { get; }
//         public InputAction Action { get; }
//
//         public Subject<InputAction.CallbackContext> OnStarted { get; } = new Subject<InputAction.CallbackContext>();
//         public Subject<InputAction.CallbackContext> OnPerformed { get; } = new Subject<InputAction.CallbackContext>();
//         public Subject<InputAction.CallbackContext> OnCanceled { get; } = new Subject<InputAction.CallbackContext>();
//
//         private readonly CompositeDisposable _disposables = new CompositeDisposable();
//
//         public InputActionWrapper(string name, InputAction action)
//         {
//             Name = name;
//             Action = action;
//
//             var performedHandle = action.performed += ctx => OnPerformed.OnNext(ctx);
//             var startedHandle = action.started += ctx => OnStarted.OnNext(ctx);
//             var canceledHandle = action.canceled += ctx => OnCanceled.OnNext(ctx);
//
//             _disposables.Add(Disposable.Create(() =>
//             {
//                 action.performed -= performedHandle;
//                 action.started -= startedHandle;
//                 action.canceled -= canceledHandle;
//             }));
//         }
//
//         public void Enable()
//         {
//             Action.Enable();
//         }
//
//         public void Disable()
//         {
//             Action.Disable();
//         }
//
//         public void Dispose()
//         {
//             Disable();
//             _disposables.Dispose();
//             OnStarted.Dispose();
//             OnPerformed.Dispose();
//             OnCanceled.Dispose();
//         }
//     }
//
//     // ====================================================================
//     // 设备绑定
//     // ====================================================================
//
//     internal class PlayerDeviceBinding : IDisposable
//     {
//         private readonly InputDevice _device;
//         private readonly PlayerInputWrapper _owner;
//
//         public PlayerDeviceBinding(InputDevice device, PlayerInputWrapper owner)
//         {
//             _device = device;
//             _owner = owner;
//
//             // 监听设备连接/断开
//             InputSystem.onDeviceChange += OnDeviceChange;
//         }
//
//         private void OnDeviceChange(InputDevice device, InputDeviceChange change)
//         {
//             if (device == _device)
//             {
//                 switch (change)
//                 {
//                     case InputDeviceChange.Disabled:
//                         _owner.Disable();
//                         break;
//                     case InputDeviceChange.Enabled:
//                         _owner.Enable();
//                         break;
//                     case InputDeviceChange.Removed:
//                         _owner.UnbindDevice(device);
//                         break;
//                 }
//             }
//         }
//
//         public void Dispose()
//         {
//             InputSystem.onDeviceChange -= OnDeviceChange;
//         }
//     }
// }
