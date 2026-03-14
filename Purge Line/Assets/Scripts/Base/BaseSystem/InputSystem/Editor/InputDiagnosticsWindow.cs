// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace Base.BaseSystem.InputSystem.Editor
// {
//     /// <summary>
//     /// 输入系统诊断窗口
//     /// 提供实时可视化的输入状态、设备信息、上下文栈和手势识别
//     /// </summary>
//     public class InputDiagnosticsWindow : EditorWindow
//     {
//         // ==================== 窗口状态 ====================
//
//         private enum Tab
//         {
//             Devices,
//             Actions,
//             Contexts,
//             Gestures,
//             Settings
//         }
//
//         private Tab _currentTab = Tab.Devices;
//         private Vector2 _scrollPosition;
//         private GUIStyle _headerStyle;
//         private GUIStyle _boxStyle;
//         private GUIStyle _labelStyle;
//         private GUIStyle _activeStyle;
//         private GUIStyle _inactiveStyle;
//         private GUIStyle _warningStyle;
//
//         // ==================== 诊断数据 ====================
//
//         private readonly Dictionary<string, InputActionDiagnosticData> _actionDiagnostics = new Dictionary<string, InputActionDiagnosticData>();
//         private readonly List<DeviceDiagnosticData> _deviceDiagnostics = new List<DeviceDiagnosticData>();
//         private readonly List<ContextDiagnosticData> _contextDiagnostics = new List<ContextDiagnosticData>();
//         private readonly List<GestureDiagnosticData> _gestureDiagnostics = new List<GestureDiagnosticData>();
//
//         // ==================== 性能统计 ====================
//
//         private float _updateTime;
//         private int _eventCount;
//         private int _deviceCount;
//         private float _averageEventLatency;
//         private readonly List<float> _latencySamples = new List<float>();
//
//         // ==================== 窗口菜单 ====================
//
//         [MenuItem("Window/Input System/Input Diagnostics")]
//         public static void ShowWindow()
//         {
//             GetWindow<InputDiagnosticsWindow>("Input Diagnostics");
//         }
//
//         // ==================== 窗口初始化 ====================
//
//         private void OnEnable()
//         {
//             // 注册回调
//             EditorApplication.update += OnEditorUpdate;
//
//             // 尝试连接到运行时输入管理器
//             if (Application.isPlaying)
//             {
//                 ConnectToRuntime();
//             }
//         }
//
//         private void OnDisable()
//         {
//             EditorApplication.update -= OnEditorUpdate;
//         }
//
//         private void ConnectToRuntime()
//         {
//             try
//             {
//                 // 这里可以通过反射或静态引用连接到运行时
//                 // 例如：_runtimeManager = InputManager.Instance;
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogWarning($"[InputDiagnostics] 无法连接到运行时: {ex.Message}");
//             }
//         }
//
//         // ==================== 更新循环 ====================
//
//         private void OnEditorUpdate()
//         {
//             if (!Application.isPlaying)
//                 return;
//
//             // 更新诊断数据
//             UpdateDeviceDiagnostics();
//             UpdateActionDiagnostics();
//             UpdateContextDiagnostics();
//             UpdateGestureDiagnostics();
//             UpdatePerformanceStats();
//
//             // 重绘窗口
//             Repaint();
//         }
//
//         // ==================== GUI绘制 ====================
//
//         private void OnGUI()
//         {
//             InitializeStyles();
//
//             // 标题栏
//             DrawHeader();
//
//             // 标签页
//             DrawTabs();
//
//             // 内容区域
//             using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
//             {
//                 _scrollPosition = scroll.scrollPosition;
//
//                 switch (_currentTab)
//                 {
//                     case Tab.Devices:
//                         DrawDevicesTab();
//                         break;
//                     case Tab.Actions:
//                         DrawActionsTab();
//                         break;
//                     case Tab.Contexts:
//                         DrawContextsTab();
//                         break;
//                     case Tab.Gestures:
//                         DrawGesturesTab();
//                         break;
//                     case Tab.Settings:
//                         DrawSettingsTab();
//                         break;
//                 }
//             }
//
//             // 底部状态栏
//             DrawStatusBar();
//         }
//
//         private void InitializeStyles()
//         {
//             _headerStyle = new GUIStyle(EditorStyles.boldLabel)
//             {
//                 fontSize = 16,
//                 padding = new RectOffset(5, 5, 5, 5)
//             };
//
//             _boxStyle = new GUIStyle(EditorStyles.helpBox)
//             {
//                 padding = new RectOffset(10, 10, 10, 10)
//             };
//
//             _labelStyle = new GUIStyle(EditorStyles.label)
//             {
//                 fontSize = 11,
//                 padding = new RectOffset(2, 2, 2, 2)
//             };
//
//             _activeStyle = new GUIStyle(_labelStyle)
//             {
//                 normal = { textColor = Color.green }
//             };
//
//             _inactiveStyle = new GUIStyle(_labelStyle)
//             {
//                 normal = { textColor = Color.gray }
//             };
//
//             _warningStyle = new GUIStyle(_labelStyle)
//             {
//                 normal = { textColor = Color.yellow }
//             };
//         }
//
//         private void DrawHeader()
//         {
//             EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
//             {
//                 GUILayout.Label("📊 Input System Diagnostics", _headerStyle);
//                 GUILayout.FlexibleSpace();
//
//                 if (Application.isPlaying)
//                 {
//                     GUI.contentColor = Color.green;
//                     GUILayout.Label("● Connected", EditorStyles.miniLabel);
//                     GUI.contentColor = Color.white;
//                 }
//                 else
//                 {
//                     GUI.contentColor = Color.gray;
//                     GUILayout.Label("○ Play Mode Required", EditorStyles.miniLabel);
//                     GUI.contentColor = Color.white;
//                 }
//             }
//             EditorGUILayout.EndHorizontal();
//
//             EditorGUILayout.Space();
//         }
//
//         private void DrawTabs()
//         {
//             EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
//             {
//                 if (GUILayout.Toggle(_currentTab == Tab.Devices, "🎮 Devices", EditorStyles.toolbarButton))
//                     _currentTab = Tab.Devices;
//                 if (GUILayout.Toggle(_currentTab == Tab.Actions, "⚡ Actions", EditorStyles.toolbarButton))
//                     _currentTab = Tab.Actions;
//                 if (GUILayout.Toggle(_currentTab == Tab.Contexts, "📚 Contexts", EditorStyles.toolbarButton))
//                     _currentTab = Tab.Contexts;
//                 if (GUILayout.Toggle(_currentTab == Tab.Gestures, "✋ Gestures", EditorStyles.toolbarButton))
//                     _currentTab = Tab.Gestures;
//                 if (GUILayout.Toggle(_currentTab == Tab.Settings, "⚙️ Settings", EditorStyles.toolbarButton))
//                     _currentTab = Tab.Settings;
//             }
//             EditorGUILayout.EndHorizontal();
//         }
//
//         private void DrawDevicesTab()
//         {
//             if (!Application.isPlaying)
//             {
//                 EditorGUILayout.HelpBox("进入 Play Mode 查看设备信息", MessageType.Info);
//                 return;
//             }
//
//             EditorGUILayout.LabelField("Connected Devices", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             foreach (var device in InputSystem.devices)
//             {
//                 DrawDeviceCard(device);
//             }
//         }
//
//         private void DrawDeviceCard(InputDevice device)
//         {
//             EditorGUILayout.BeginVertical(_boxStyle);
//             {
//                 EditorGUILayout.BeginHorizontal();
//                 {
//                     // 设备图标
//                     var icon = GetDeviceIcon(device);
//                     GUILayout.Label(icon, GUILayout.Width(32), GUILayout.Height(32));
//
//                     // 设备信息
//                     EditorGUILayout.BeginVertical();
//                     {
//                         EditorGUILayout.LabelField(device.name, EditorStyles.boldLabel);
//                         EditorGUILayout.LabelField($"Type: {device.GetType().Name}", EditorStyles.miniLabel);
//                         EditorGUILayout.LabelField($"Id: {device.deviceId}", EditorStyles.miniLabel);
//                     }
//                     EditorGUILayout.EndVertical();
//
//                     GUILayout.FlexibleSpace();
//
//                     // 状态指示器
//                     var style = device.enabled ? _activeStyle : _inactiveStyle;
//                     EditorGUILayout.LabelField(device.enabled ? "Active" : "Inactive", style);
//                 }
//                 EditorGUILayout.EndHorizontal();
//
//                 EditorGUILayout.Space();
//
//                 // 设备详情（可折叠）
//                 if (device is Gamepad gamepad)
//                 {
//                     DrawGamepadDetails(gamepad);
//                 }
//                 else if (device is Keyboard keyboard)
//                 {
//                     DrawKeyboardDetails(keyboard);
//                 }
//                 else if (device is Mouse mouse)
//                 {
//                     DrawMouseDetails(mouse);
//                 }
//             }
//             EditorGUILayout.EndVertical();
//             EditorGUILayout.Space();
//         }
//
//         private void DrawGamepadDetails(Gamepad gamepad)
//         {
//             EditorGUILayout.BeginHorizontal();
//             {
//                 EditorGUILayout.LabelField("Left Stick:", gamepad.leftStick.ReadValue().ToString("F3"));
//                 EditorGUILayout.LabelField("Right Stick:", gamepad.rightStick.ReadValue().ToString("F3"));
//             }
//             EditorGUILayout.EndHorizontal();
//
//             EditorGUILayout.BeginHorizontal();
//             {
//                 EditorGUILayout.LabelField("D-Pad:", gamepad.dpad.ReadValue().ToString());
//                 EditorGUILayout.LabelField("Buttons:", GetGamepadButtons(gamepad));
//             }
//             EditorGUILayout.EndHorizontal();
//         }
//
//         private void DrawKeyboardDetails(Keyboard keyboard)
//         {
//             var pressedKeys = keyboard.allKeys.Where(k => k.isPressed).Select(k => k.displayName).ToList();
//             if (pressedKeys.Count > 0)
//             {
//                 EditorGUILayout.LabelField("Pressed Keys:", string.Join(", ", pressedKeys));
//             }
//         }
//
//         private void DrawMouseDetails(Mouse mouse)
//         {
//             EditorGUILayout.BeginHorizontal();
//             {
//                 EditorGUILayout.LabelField("Position:", mouse.position.ReadValue().ToString("F0"));
//                 EditorGUILayout.LabelField("Scroll:", mouse.scroll.ReadValue().ToString("F1"));
//             }
//             EditorGUILayout.EndHorizontal();
//         }
//
//         private string GetGamepadButtons(Gamepad gamepad)
//         {
//             var buttons = new List<string>();
//             if (gamepad.aButton.isPressed) buttons.Add("A");
//             if (gamepad.bButton.isPressed) buttons.Add("B");
//             if (gamepad.xButton.isPressed) buttons.Add("X");
//             if (gamepad.yButton.isPressed) buttons.Add("Y");
//             if (gamepad.leftShoulder.isPressed) buttons.Add("LB");
//             if (gamepad.rightShoulder.isPressed) buttons.Add("RB");
//             if (gamepad.startButton.isPressed) buttons.Add("Start");
//             if (gamepad.selectButton.isPressed) buttons.Add("Select");
//             return buttons.Count > 0 ? string.Join(", ", buttons) : "None";
//         }
//
//         private GUIContent GetDeviceIcon(InputDevice device)
//         {
//             if (device is Gamepad) return EditorGUIUtility.IconContent("Gamepad Icon");
//             if (device is Keyboard) return EditorGUIUtility.IconContent("Keyboard Icon");
//             if (device is Mouse) return EditorGUIUtility.IconContent("Mouse Icon");
//             if (device is Touchscreen) return EditorGUIUtility.IconContent("Touch Icon");
//             return EditorGUIUtility.IconContent("DefaultAsset Icon");
//         }
//
//         private void DrawActionsTab()
//         {
//             if (!Application.isPlaying)
//             {
//                 EditorGUILayout.HelpBox("进入 Play Mode 查看输入动作", MessageType.Info);
//                 return;
//             }
//
//             EditorGUILayout.LabelField("Input Actions", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             // 这里可以列出所有输入动作和它们的状态
//             EditorGUILayout.HelpBox("输入动作监控功能待实现", MessageType.Info);
//         }
//
//         private void DrawContextsTab()
//         {
//             if (!Application.isPlaying)
//             {
//                 EditorGUILayout.HelpBox("进入 Play Mode 查看输入上下文", MessageType.Info);
//                 return;
//             }
//
//             EditorGUILayout.LabelField("Input Context Stack", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             // 这里可以显示上下文栈的可视化
//             EditorGUILayout.HelpBox("输入上下文监控功能待实现", MessageType.Info);
//         }
//
//         private void DrawGesturesTab()
//         {
//             if (!Application.isPlaying)
//             {
//                 EditorGUILayout.HelpBox("进入 Play Mode 查看手势识别", MessageType.Info);
//                 return;
//             }
//
//             EditorGUILayout.LabelField("Gesture Recognition", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             // 这里可以显示手势识别的状态
//             EditorGUILayout.HelpBox("手势识别监控功能待实现", MessageType.Info);
//         }
//
//         private void DrawSettingsTab()
//         {
//             EditorGUILayout.LabelField("Diagnostics Settings", EditorStyles.boldLabel);
//             EditorGUILayout.Space();
//
//             // 配置加载
//             var config = LoadOrCreateConfig();
//             if (config != null)
//             {
//                 EditorGUILayout.ObjectField("Current Config:", config, typeof(InputConfig), false);
//                 if (GUILayout.Button("Open Config"))
//                 {
//                     Selection.activeObject = config;
//                 }
//             }
//
//             EditorGUILayout.Space();
//
//             // 诊断选项
//             EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
//             EditorGUILayout.Toggle("Show Device Status", true);
//             EditorGUILayout.Toggle("Show Input Events", false);
//             EditorGUILayout.Toggle("Show Context Stack", true);
//             EditorGUILayout.Toggle("Show Gestures", false);
//
//             EditorGUILayout.Space();
//
//             // 性能选项
//             EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
//             EditorGUILayout.Toggle("Collect Latency Stats", false);
//             EditorGUILayout.IntSlider("Sample Count", 60, 10, 300);
//         }
//
//         private void DrawStatusBar()
//         {
//             EditorGUILayout.Space();
//             EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
//             {
//                 if (Application.isPlaying)
//                 {
//                     EditorGUILayout.LabelField($"Devices: {InputSystem.devices.Count}");
//                     EditorGUILayout.LabelField($"Update: {_updateTime:F1}ms");
//                     EditorGUILayout.LabelField($"Events: {_eventCount}");
//                     EditorGUILayout.LabelField($"Latency: {_averageEventLatency:F1}ms");
//                 }
//                 else
//                 {
//                     EditorGUILayout.LabelField("Enter Play Mode for diagnostics");
//                 }
//             }
//             EditorGUILayout.EndHorizontal();
//         }
//
//         // ==================== 诊断数据更新 ====================
//
//         private void UpdateDeviceDiagnostics()
//         {
//             _deviceCount = InputSystem.devices.Count;
//         }
//
//         private void UpdateActionDiagnostics()
//         {
//             // 可以在这里收集输入动作的诊断数据
//         }
//
//         private void UpdateContextDiagnostics()
//         {
//             // 可以在这里收集输入上下文的诊断数据
//         }
//
//         private void UpdateGestureDiagnostics()
//         {
//             // 可以在这里收集手势识别的诊断数据
//         }
//
//         private void UpdatePerformanceStats()
//         {
//             // 可以在这里收集性能统计数据
//         }
//
//         // ==================== 辅助方法 ====================
//
//         private InputConfig LoadOrCreateConfig()
//         {
//             var config = AssetDatabase.LoadAssetAtPath<InputConfig>("Assets/Resources/InputSystem/DefaultInputConfig.asset");
//             if (config == null)
//             {
//                 var path = "Assets/Resources/InputSystem";
//                 if (!System.IO.Directory.Exists(path))
//                 {
//                     System.IO.Directory.CreateDirectory(path);
//                 }
//
//                 config = CreateInstance<InputConfig>();
//                 AssetDatabase.CreateAsset(config, $"{path}/DefaultInputConfig.asset");
//                 AssetDatabase.SaveAssets();
//             }
//             return config;
//         }
//     }
//
//     // ====================================================================
//     // 诊断数据结构
//     // ====================================================================
//
//     public struct InputActionDiagnosticData
//     {
//         public string Name;
//         public bool IsActive;
//         public float LastPerformedTime;
//         public int PerformCount;
//         public float AverageValue;
//     }
//
//     public struct DeviceDiagnosticData
//     {
//         public string Name;
//         public string Type;
//         public bool IsEnabled;
//         public float LastActiveTime;
//     }
//
//     public struct ContextDiagnosticData
//     {
//         public string Name;
//         public bool IsActive;
//         public int StackPosition;
//     }
//
//     public struct GestureDiagnosticData
//     {
//         public string Name;
//         public bool IsActive;
//         public float Progress;
//         public int RecognitionCount;
//     }
// }
// #endif
