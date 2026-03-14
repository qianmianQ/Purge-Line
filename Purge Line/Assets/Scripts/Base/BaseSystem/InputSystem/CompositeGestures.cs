// using System;
// using System.Collections.Generic;
// using R3;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     // ====================================================================
//     // 手势基类
//     // ====================================================================
//
//     /// <summary>
//     /// 手势基类
//     /// </summary>
//     public abstract class InputGesture : IDisposable
//     {
//         public abstract string Name { get; }
//         public abstract bool IsActive { get; }
//         public abstract bool IsCompleted { get; }
//
//         public abstract IObservable<GestureResult> OnGestureRecognized { get; }
//
//         public virtual void Reset()
//         {
//         }
//
//         public abstract void ProcessInput(InputAction.CallbackContext context);
//
//         public virtual void Update(float deltaTime)
//         {
//         }
//
//         public abstract void Dispose();
//     }
//
//     // ====================================================================
//     // 手势结果
//     // ====================================================================
//
//     public struct GestureResult
//     {
//         public GestureType Type;
//         public float Duration;
//         public Vector2 StartPosition;
//         public Vector2 EndPosition;
//         public Vector2 Direction;
//         public float Distance;
//         public InputDevice Device;
//         public object ContextData;
//     }
//
//     public enum GestureType
//     {
//         Unknown,
//         Click,
//         DoubleClick,
//         LongPress,
//         Drag,
//         Swipe,
//         Pinch,
//         Rotate
//     }
//
//     // ====================================================================
//     // 双击手势
//     // ====================================================================
//
//     public class DoubleClickGesture : InputGesture
//     {
//         // ==================== 配置 ====================
//
//         public float MaximumInterval { get; set; } = 0.5f;
//         public float MaximumDistance { get; set; } = 50f;
//
//         // ==================== 状态 ====================
//
//         private bool _isActive;
//         private bool _isCompleted;
//         private Vector2 _lastPosition;
//         private float _lastTime;
//         private readonly Subject<GestureResult> _onGestureRecognized = new Subject<GestureResult>();
//
//         public override string Name => "DoubleClick";
//         public override bool IsActive => _isActive;
//         public override bool IsCompleted => _isCompleted;
//         public override IObservable<GestureResult> OnGestureRecognized => _onGestureRecognized.AsObservable();
//
//         public override void Reset()
//         {
//             _isActive = false;
//             _isCompleted = false;
//         }
//
//         public override void ProcessInput(InputAction.CallbackContext context)
//         {
//             if (context.phase != InputActionPhase.Performed)
//                 return;
//
//             var position = GetPositionFromContext(context);
//             var time = Time.time;
//
//             if (_isActive)
//             {
//                 // 检查是否符合双击条件
//                 if (time - _lastTime <= MaximumInterval && Vector2.Distance(position, _lastPosition) <= MaximumDistance)
//                 {
//                     _isCompleted = true;
//                     var result = new GestureResult
//                     {
//                         Type = GestureType.DoubleClick,
//                         Duration = time - _lastTime,
//                         StartPosition = _lastPosition,
//                         EndPosition = position,
//                         Distance = Vector2.Distance(_lastPosition, position),
//                         Device = context.control.device
//                     };
//                     _onGestureRecognized.OnNext(result);
//                 }
//                 _isActive = false;
//             }
//             else
//             {
//                 // 第一次点击
//                 _isActive = true;
//                 _lastPosition = position;
//                 _lastTime = time;
//             }
//         }
//
//         private static Vector2 GetPositionFromContext(InputAction.CallbackContext context)
//         {
//             if (context.action.type == InputActionType.Value)
//             {
//                 var value = context.ReadValue<Vector2>();
//                 if (value != Vector2.zero)
//                     return value;
//             }
//
//             // 尝试从设备获取位置
//             if (context.control.device is Mouse mouse)
//                 return mouse.position.ReadValue();
//             if (context.control.device is Touchscreen touch)
//                 return touch.primaryTouch.position.ReadValue();
//
//             return Vector2.zero;
//         }
//
//         public override void Update(float deltaTime)
//         {
//             if (_isActive && Time.time - _lastTime > MaximumInterval)
//             {
//                 _isActive = false;
//             }
//         }
//
//         public override void Dispose()
//         {
//             _onGestureRecognized.Dispose();
//         }
//     }
//
//     // ====================================================================
//     // 长按手势
//     // ====================================================================
//
//     public class LongPressGesture : InputGesture
//     {
//         // ==================== 配置 ====================
//
//         public float MinimumDuration { get; set; } = 0.5f;
//         public float MaximumMovement { get; set; } = 10f;
//
//         // ==================== 状态 ====================
//
//         private bool _isActive;
//         private bool _isCompleted;
//         private Vector2 _startPosition;
//         private float _startTime;
//         private bool _recognized;
//         private readonly Subject<GestureResult> _onGestureRecognized = new Subject<GestureResult>();
//
//         public override string Name => "LongPress";
//         public override bool IsActive => _isActive;
//         public override bool IsCompleted => _isCompleted;
//         public override IObservable<GestureResult> OnGestureRecognized => _onGestureRecognized.AsObservable();
//
//         public override void Reset()
//         {
//             _isActive = false;
//             _isCompleted = false;
//             _recognized = false;
//         }
//
//         public override void ProcessInput(InputAction.CallbackContext context)
//         {
//             switch (context.phase)
//             {
//                 case InputActionPhase.Started:
//                     _isActive = true;
//                     _startPosition = GetPositionFromContext(context);
//                     _startTime = Time.time;
//                     _recognized = false;
//                     break;
//                 case InputActionPhase.Performed:
//                     if (_isActive && !_recognized)
//                     {
//                         var position = GetPositionFromContext(context);
//                         var duration = Time.time - _startTime;
//                         var distance = Vector2.Distance(_startPosition, position);
//
//                         if (duration >= MinimumDuration && distance <= MaximumMovement)
//                         {
//                             _recognized = true;
//                             _isCompleted = true;
//                             var result = new GestureResult
//                             {
//                                 Type = GestureType.LongPress,
//                                 Duration = duration,
//                                 StartPosition = _startPosition,
//                                 EndPosition = position,
//                                 Distance = distance,
//                                 Device = context.control.device
//                             };
//                             _onGestureRecognized.OnNext(result);
//                         }
//                     }
//                     break;
//                 case InputActionPhase.Canceled:
//                     _isActive = false;
//                     _isCompleted = false;
//                     _recognized = false;
//                     break;
//             }
//         }
//
//         private static Vector2 GetPositionFromContext(InputAction.CallbackContext context)
//         {
//             if (context.action.type == InputActionType.Value)
//                 return context.ReadValue<Vector2>();
//             if (context.control.device is Mouse mouse)
//                 return mouse.position.ReadValue();
//             if (context.control.device is Touchscreen touch)
//                 return touch.primaryTouch.position.ReadValue();
//             return Vector2.zero;
//         }
//
//         public override void Update(float deltaTime)
//         {
//         }
//
//         public override void Dispose()
//         {
//             _onGestureRecognized.Dispose();
//         }
//     }
//
//     // ====================================================================
//     // 拖拽手势
//     // ====================================================================
//
//     public class DragGesture : InputGesture
//     {
//         // ==================== 配置 ====================
//
//         public float MinimumDistance { get; set; } = 10f;
//         public float MinimumDuration { get; set; } = 0.1f;
//
//         // ==================== 状态 ====================
//
//         private bool _isActive;
//         private bool _isCompleted;
//         private Vector2 _startPosition;
//         private Vector2 _lastPosition;
//         private float _startTime;
//         private bool _hasDragged;
//         private readonly Subject<GestureResult> _onGestureRecognized = new Subject<GestureResult>();
//
//         public override string Name => "Drag";
//         public override bool IsActive => _isActive;
//         public override bool IsCompleted => _isCompleted;
//         public override IObservable<GestureResult> OnGestureRecognized => _onGestureRecognized.AsObservable();
//
//         public override void Reset()
//         {
//             _isActive = false;
//             _isCompleted = false;
//             _hasDragged = false;
//         }
//
//         public override void ProcessInput(InputAction.CallbackContext context)
//         {
//             switch (context.phase)
//             {
//                 case InputActionPhase.Started:
//                     _isActive = true;
//                     _startPosition = GetPositionFromContext(context);
//                     _lastPosition = _startPosition;
//                     _startTime = Time.time;
//                     _hasDragged = false;
//                     break;
//                 case InputActionPhase.Performed:
//                     if (_isActive)
//                     {
//                         var position = GetPositionFromContext(context);
//                         var distance = Vector2.Distance(_startPosition, position);
//                         var duration = Time.time - _startTime;
//
//                         if (!_hasDragged && distance >= MinimumDistance && duration >= MinimumDuration)
//                         {
//                             _hasDragged = true;
//                             _isCompleted = true;
//                             var result = new GestureResult
//                             {
//                                 Type = GestureType.Drag,
//                                 Duration = duration,
//                                 StartPosition = _startPosition,
//                                 EndPosition = position,
//                                 Direction = (position - _startPosition).normalized,
//                                 Distance = distance,
//                                 Device = context.control.device
//                             };
//                             _onGestureRecognized.OnNext(result);
//                         }
//
//                         _lastPosition = position;
//                     }
//                     break;
//                 case InputActionPhase.Canceled:
//                     _isActive = false;
//                     _isCompleted = false;
//                     _hasDragged = false;
//                     break;
//             }
//         }
//
//         private static Vector2 GetPositionFromContext(InputAction.CallbackContext context)
//         {
//             if (context.action.type == InputActionType.Value)
//                 return context.ReadValue<Vector2>();
//             if (context.control.device is Mouse mouse)
//                 return mouse.position.ReadValue();
//             if (context.control.device is Touchscreen touch)
//                 return touch.primaryTouch.position.ReadValue();
//             return Vector2.zero;
//         }
//
//         public override void Update(float deltaTime)
//         {
//         }
//
//         public override void Dispose()
//         {
//             _onGestureRecognized.Dispose();
//         }
//     }
//
//     // ====================================================================
//     // 滑动手势
//     // ====================================================================
//
//     public class SwipeGesture : InputGesture
//     {
//         // ==================== 配置 ====================
//
//         public float MinimumDistance { get; set; } = 50f;
//         public float MaximumDuration { get; set; } = 0.5f;
//         public float MinimumVelocity { get; set; } = 100f;
//
//         // ==================== 状态 ====================
//
//         private bool _isActive;
//         private bool _isCompleted;
//         private Vector2 _startPosition;
//         private Vector2 _lastPosition;
//         private float _startTime;
//         private readonly Subject<GestureResult> _onGestureRecognized = new Subject<GestureResult>();
//
//         public override string Name => "Swipe";
//         public override bool IsActive => _isActive;
//         public override bool IsCompleted => _isCompleted;
//         public override IObservable<GestureResult> OnGestureRecognized => _onGestureRecognized.AsObservable();
//
//         public override void Reset()
//         {
//             _isActive = false;
//             _isCompleted = false;
//         }
//
//         public override void ProcessInput(InputAction.CallbackContext context)
//         {
//             switch (context.phase)
//             {
//                 case InputActionPhase.Started:
//                     _isActive = true;
//                     _startPosition = GetPositionFromContext(context);
//                     _lastPosition = _startPosition;
//                     _startTime = Time.time;
//                     break;
//                 case InputActionPhase.Performed:
//                     if (_isActive)
//                     {
//                         _lastPosition = GetPositionFromContext(context);
//                     }
//                     break;
//                 case InputActionPhase.Canceled:
//                     if (_isActive)
//                     {
//                         var distance = Vector2.Distance(_startPosition, _lastPosition);
//                         var duration = Time.time - _startTime;
//
//                         if (distance >= MinimumDistance && duration <= MaximumDuration)
//                         {
//                             var velocity = distance / duration;
//                             if (velocity >= MinimumVelocity)
//                             {
//                                 _isCompleted = true;
//                                 var result = new GestureResult
//                                 {
//                                     Type = GestureType.Swipe,
//                                     Duration = duration,
//                                     StartPosition = _startPosition,
//                                     EndPosition = _lastPosition,
//                                     Direction = (_lastPosition - _startPosition).normalized,
//                                     Distance = distance,
//                                     Device = context.control.device
//                                 };
//                                 _onGestureRecognized.OnNext(result);
//                             }
//                         }
//                     }
//                     _isActive = false;
//                     break;
//             }
//         }
//
//         private static Vector2 GetPositionFromContext(InputAction.CallbackContext context)
//         {
//             if (context.action.type == InputActionType.Value)
//                 return context.ReadValue<Vector2>();
//             if (context.control.device is Mouse mouse)
//                 return mouse.position.ReadValue();
//             if (context.control.device is Touchscreen touch)
//                 return touch.primaryTouch.position.ReadValue();
//             return Vector2.zero;
//         }
//
//         public override void Update(float deltaTime)
//         {
//         }
//
//         public override void Dispose()
//         {
//             _onGestureRecognized.Dispose();
//         }
//     }
//
//     // ====================================================================
//     // 手势识别器工厂
//     // ====================================================================
//
//     public static class GestureRecognizer
//     {
//         public static InputGesture Create(GestureType type)
//         {
//             return type switch
//             {
//                 GestureType.DoubleClick => new DoubleClickGesture(),
//                 GestureType.LongPress => new LongPressGesture(),
//                 GestureType.Drag => new DragGesture(),
//                 GestureType.Swipe => new SwipeGesture(),
//                 _ => null
//             };
//         }
//     }
// }
