# 工业级响应式输入系统架构

## 概述

基于 Unity InputSystem 1.14.2 和 R3 响应式编程库的高性能输入系统。旨在提供：

- **极致性能**：零 GC（或低 GC），线程安全
- **响应式设计**：基于 R3 的响应式事件流
- **上下文隔离**：支持输入上下文栈，实现界面与游戏玩法的隔离
- **本地多人**：支持多设备、多玩家输入
- **设备管理**：自动检测、设备锁定、智能热插拔
- **复合手势**：内置双击、长按、拖拽等复杂输入识别
- **可扩展**：可自定义输入操作和手势类型
- **配置驱动**：支持数据/配置驱动，可视化调整

## 架构层次

```
InputSystem Layer
├── Core
│   ├── InputManager (单例，DependencyManager 集成)
│   ├── InputContextStack (上下文管理)
│   └── DeviceManager (设备管理)
├── InputActions
│   ├── InputActionMapWrapper (操作映射封装)
│   ├── InputActionWrapper (单个操作封装)
│   └── InputBindingWrapper (绑定封装)
├── InputContexts
│   ├── InputContextBase (上下文基类)
│   ├── UIInputContext (UI 上下文)
│   ├── GameplayInputContext (游戏玩法上下文)
│   └── GlobalInputContext (全局上下文)
├── CompositeGestures
│   ├── DoubleClickGesture (双击)
│   ├── LongPressGesture (长按)
│   ├── DragGesture (拖拽)
│   └── CustomGestureBase (自定义手势基类)
├── DeviceSpecific
│   ├── KeyboardInput (键盘)
│   ├── MouseInput (鼠标)
│   ├── GamepadInput (手柄)
│   └── TouchInput (触摸)
└── Utilities
    ├── InputLogger (日志记录)
    ├── InputDiagnostics (诊断工具)
    └── InputExtensions (扩展方法)
```

## 核心特性

### 1. 响应式输入流

```csharp
// 获取输入操作的 Observable 流
InputManager.Instance.GetAction<PlayerInputActions>(x => x.Move)
    .OnPerformed()
    .Subscribe(data => Debug.Log($"Move: {data.ReadValue<Vector2>()}"));

// 手势识别
InputManager.Instance.GetAction<PlayerInputActions>(x => x.Click)
    .OnPerformed()
    .DetectDoubleClick()
    .Subscribe(data => Debug.Log("Double Click Detected"));
```

### 2. 输入上下文栈

```csharp
// 入栈/出栈上下文
InputManager.Instance.PushContext<MenuInputContext>();
InputManager.Instance.PopContext<MenuInputContext>();

// 设备锁定到特定上下文
InputManager.Instance.LockDevice(device, typeof(MenuInputContext));
```

### 3. 本地多人支持

```csharp
// 创建多玩家输入配置
var player1 = InputManager.Instance.CreatePlayerInput(0);
var player2 = InputManager.Instance.CreatePlayerInput(1);

// 分别监听不同玩家的输入
player1.GetAction<PlayerInputActions>(x => x.Move)
    .OnPerformed()
    .Subscribe(data => Debug.Log($"Player 1 Move: {data.ReadValue<Vector2>()}"));

player2.GetAction<PlayerInputActions>(x => x.Move)
    .OnPerformed()
    .Subscribe(data => Debug.Log($"Player 2 Move: {data.ReadValue<Vector2>()}"));
```

### 4. 复合手势

```csharp
// 长按手势（至少 500ms）
InputManager.Instance.GetAction<PlayerInputActions>(x => x.Hold)
    .OnPerformed()
    .DetectLongPress(500)
    .Subscribe(data => Debug.Log("Long Press Detected"));

// 拖拽手势
InputManager.Instance.GetAction<PlayerInputActions>(x => x.Drag)
    .OnPerformed()
    .DetectDrag()
    .Subscribe(dragData => Debug.Log($"Dragged from {dragData.Start} to {dragData.End}"));
```

### 5. 设备管理与热插拔

```csharp
// 设备状态监听
InputManager.Instance.OnDeviceConnected
    .Subscribe(device => Debug.Log($"Device connected: {device.name}"));

InputManager.Instance.OnDeviceDisconnected
    .Subscribe(device => Debug.Log($"Device disconnected: {device.name}"));

// 强制设备类型（如只接受键盘）
InputManager.Instance.FilterDevices(device => device is Keyboard);
```

### 6. 配置驱动

```csharp
// 从配置文件加载
var config = InputConfig.Load("InputConfig");
InputManager.Instance.ApplyConfig(config);

// 可视化调整
InputDiagnosticsWindow.Show();
```

## 集成方式

### 1. 系统注册

```csharp
// 在 GameFramework.Initialize() 中注册
DependencyManager.Instance.Register(new InputManager());
```

### 2. InputActions 定义

创建脚本化 InputActions 资产，如 `PlayerInputActions`，并在编辑器中配置。

### 3. 输入上下文创建

```csharp
public class MenuInputContext : InputContextBase
{
    public MenuInputContext() : base("Menu") { }

    protected override void OnContextActivated()
    {
        // 启用菜单相关操作
    }

    protected override void OnContextDeactivated()
    {
        // 禁用菜单相关操作
    }
}
```

## 性能优化

- **对象池**：预创建 InputActionState 对象，避免 GC
- **事件流缓存**：缓存常用 Observable 流
- **增量更新**：只处理发生变化的输入
- **Burst 兼容**：支持 Burst 编译的输入处理

## 调试与诊断

- **实时可视化**：`InputDiagnosticsWindow` 显示输入事件、设备状态
- **输入重放**：录制和重放输入序列
- **性能分析**：输入处理耗时统计

## 兼容性

- Unity InputSystem 1.14.2+
- 支持键盘、鼠标、手柄、触摸
- 支持本地多人游戏（最多 4 人）
- 支持热插拔和设备切换
