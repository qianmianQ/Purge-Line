// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem
// {
//     /// <summary>
//     /// 输入系统配置
//     /// 支持数据/配置驱动的输入管理
//     /// </summary>
//     [CreateAssetMenu(fileName = "InputConfig", menuName = "Input System/Input Config", order = 1)]
//     public class InputConfig : ScriptableObject
//     {
//         // ==================== 基本配置 ====================
//
//         [Header("基本配置")]
//         [Tooltip("输入系统名称")]
//         public string Name = "Default Input Config";
//
//         [Tooltip("输入采样频率（Hz）")]
//         [Range(30, 120)]
//         public int SamplingRate = 60;
//
//         [Tooltip("输入预测缓冲区大小（毫秒）")]
//         [Range(0, 100)]
//         public int PredictionBufferSize = 30;
//
//         // ==================== 输入上下文配置 ====================
//
//         [Header("输入上下文")]
//         [Tooltip("启用上下文隔离")]
//         public bool EnableContextIsolation = true;
//
//         [Tooltip("默认上下文切换时间（秒）")]
//         [Range(0, 1)]
//         public float ContextSwitchTime = 0.1f;
//
//         [Tooltip("启用输入缓冲")]
//         public bool EnableInputBuffering = true;
//
//         [Tooltip("输入缓冲时间（秒）")]
//         [Range(0, 0.5f)]
//         public float InputBufferTime = 0.1f;
//
//         // ==================== 设备配置 ====================
//
//         [Header("设备配置")]
//         [Tooltip("自动设备检测")]
//         public bool AutoDeviceDetection = true;
//
//         [Tooltip("设备轮询间隔（毫秒）")]
//         [Range(100, 1000)]
//         public int DevicePollingInterval = 500;
//
//         [Tooltip("设备超时（秒）")]
//         [Range(1, 60)]
//         public int DeviceTimeout = 5;
//
//         [Tooltip("启用设备锁定")]
//         public bool EnableDeviceLocking = true;
//
//         [Tooltip("设备锁定超时（秒）")]
//         [Range(1, 30)]
//         public int DeviceLockTimeout = 5;
//
//         // ==================== 本地多人配置 ====================
//
//         [Header("本地多人")]
//         [Tooltip("最大玩家数量")]
//         [Range(1, 8)]
//         public int MaxPlayers = 4;
//
//         [Tooltip("自动分配设备")]
//         public bool AutoAssignDevices = true;
//
//         [Tooltip("允许共享设备（键盘/鼠标）")]
//         public bool AllowSharedDevices = true;
//
//         [Tooltip("玩家设备绑定模式")]
//         public DeviceBindingMode DeviceBindingMode = DeviceBindingMode.Persistent;
//
//         // ==================== 手势识别配置 ====================
//
//         [Header("手势识别")]
//         [Tooltip("启用手势识别")]
//         public bool EnableGestureRecognition = true;
//
//         [Tooltip("双击间隔（秒）")]
//         [Range(0.1f, 1.0f)]
//         public float DoubleClickInterval = 0.5f;
//
//         [Tooltip("双击距离阈值（像素）")]
//         [Range(0, 100)]
//         public float DoubleClickDistance = 50;
//
//         [Tooltip("长按持续时间（秒）")]
//         [Range(0.1f, 2.0f)]
//         public float LongPressDuration = 0.5f;
//
//         [Tooltip("长按移动阈值（像素）")]
//         [Range(0, 50)]
//         public float LongPressMovement = 10;
//
//         [Tooltip("拖拽距离阈值（像素）")]
//         [Range(0, 100)]
//         public float DragDistance = 10;
//
//         [Tooltip("拖拽持续时间阈值（秒）")]
//         [Range(0.1f, 1.0f)]
//         public float DragDuration = 0.1f;
//
//         [Tooltip("滑动距离阈值（像素）")]
//         [Range(0, 200)]
//         public float SwipeDistance = 50;
//
//         [Tooltip("滑动持续时间阈值（秒）")]
//         [Range(0.1f, 1.0f)]
//         public float SwipeDuration = 0.5f;
//
//         [Tooltip("滑动速度阈值（像素/秒）")]
//         [Range(0, 1000)]
//         public float SwipeVelocity = 100;
//
//         // ==================== 调试配置 ====================
//
//         [Header("调试")]
//         [Tooltip("启用调试模式")]
//         public bool EnableDebug = false;
//
//         [Tooltip("输入事件可视化")]
//         public bool VisualizeInputEvents = false;
//
//         [Tooltip("设备状态可视化")]
//         public bool VisualizeDeviceStatus = false;
//
//         [Tooltip("上下文状态可视化")]
//         public bool VisualizeContextStatus = false;
//
//         [Tooltip("手势识别可视化")]
//         public bool VisualizeGestures = false;
//
//         [Tooltip("输入日志级别")]
//         public LogLevel InputLogLevel = LogLevel.Warning;
//
//         // ==================== 输入动作配置 ====================
//
//         [Header("输入动作")]
//         public List<ActionConfig> Actions = new List<ActionConfig>();
//
//         // ==================== 访问器方法 ====================
//
//         public ActionConfig GetActionConfig(string name)
//         {
//             return Actions.Find(x => x.Name == name);
//         }
//
//         public bool HasAction(string name)
//         {
//             return Actions.Exists(x => x.Name == name);
//         }
//
//         // ==================== 加载方法 ====================
//
//         public static InputConfig LoadDefault()
//         {
//             // 尝试从 Resources 加载默认配置
//             var config = Resources.Load<InputConfig>("InputSystem/DefaultInputConfig");
//             if (config == null)
//             {
//                 // 创建默认配置
//                 config = ScriptableObject.CreateInstance<InputConfig>();
//                 config.Name = "Runtime Default";
//             }
//             return config;
//         }
//
//         public static InputConfig Load(string path)
//         {
//             var config = Resources.Load<InputConfig>(path);
//             if (config == null)
//             {
//                 Debug.LogWarning($"[InputConfig] 配置文件未找到: {path}，使用默认配置");
//                 config = LoadDefault();
//             }
//             return config;
//         }
//     }
//
//     // ====================================================================
//     // 动作配置
//     // ====================================================================
//
//     [Serializable]
//     public class ActionConfig
//     {
//         [Tooltip("动作名称")]
//         public string Name = "New Action";
//
//         [Tooltip("输入动作类型")]
//         public InputActionType Type = InputActionType.Button;
//
//         [Tooltip("是否启用")]
//         public bool Enabled = true;
//
//         [Tooltip("动作行为")]
//         public InputActionBehavior Behavior = InputActionBehavior.PressOnly;
//
//         [Tooltip("轮询模式")]
//         public InputActionPollingMode PollingMode = InputActionPollingMode.Auto;
//
//         [Tooltip("敏感度（值范围）")]
//         [Range(0.1f, 5f)]
//         public float Sensitivity = 1.0f;
//
//         [Tooltip("死区（值范围）")]
//         [Range(0, 1)]
//         public float DeadZone = 0.05f;
//
//         [Tooltip("响应曲线")]
//         public ResponseCurveType ResponseCurve = ResponseCurveType.Linear;
//
//         [Tooltip("绑定")]
//         public List<BindingConfig> Bindings = new List<BindingConfig>();
//     }
//
//     // ====================================================================
//     // 绑定配置
//     // ====================================================================
//
//     [Serializable]
//     public class BindingConfig
//     {
//         [Tooltip("绑定路径")]
//         public string Path = "<Keyboard>/space";
//
//         [Tooltip("设备类型筛选")]
//         public InputDeviceType TargetDeviceType = InputDeviceType.All;
//
//         [Tooltip("绑定权重")]
//         [Range(0, 1)]
//         public float Weight = 1.0f;
//
//         [Tooltip("是否启用")]
//         public bool Enabled = true;
//     }
//
//     // ====================================================================
//     // 设备绑定模式
//     // ====================================================================
//
//     public enum DeviceBindingMode
//     {
//         [Tooltip("设备绑定到玩家直到游戏结束")]
//         Persistent,
//
//         [Tooltip("设备在空闲时可以重新分配")]
//         Dynamic,
//
//         [Tooltip("设备必须通过交互绑定")]
//         Interactive
//     }
//
//     // ====================================================================
//     // 响应曲线类型
//     // ====================================================================
//
//     public enum ResponseCurveType
//     {
//         Linear,
//         SmoothStep,
//         Exponential,
//         Logarithmic
//     }
// }
