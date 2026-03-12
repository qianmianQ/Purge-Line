# Event System Fixes Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复事件系统的 6 个 Critical/Important 问题和 5 个 Minor 问题，使其达到工业级标准。

**Architecture:** 三层结构保持不变（EventBus → EventDomain → EventSystem），但每个 EventDomain 持有独立的 EventBus 实例以实现真正的域隔离；EventBus 的 enum 路径通过独立泛型方法彻底消除装箱；所有公共类型迁移至 `PurgeLine.Events` 命名空间。

**Tech Stack:** C# 10, Unity 2022.3, R3 1.3.0, System.Runtime.CompilerServices.Unsafe

---

## 修复问题对照表

| Issue | 严重性 | 涉及 Task |
|---|---|---|
| C-1: `Convert.ToInt32` 装箱热路径 | Critical | Task 1 |
| C-2: `_disposed` 是死字段，无守卫 | Critical | Task 1 |
| C-3: 有参事件域隔离是虚假的 | Critical | Task 2, 3 |
| C-4: `EventSystem.Dispose()` 不会被自动调用 | Critical | Task 5 |
| I-1: Dispose 清理两个池策略不对称 | Important | Task 1 |
| I-2: `GetOrCreate<T>()` 泄露 Subject 引用 | Important | Task 1 |
| I-3: `None=0` 可被订阅/派发 | Important | Task 2 |
| I-4: 无命名空间，污染全局 | Important | Task 1-4 |
| M-1: 注释死代码 `EmitEnum` | Minor | Task 1 |
| M-2: `EventSystem` 缺少 `IsDisposed` | Minor | Task 3 |
| M-3: `EventExample` 占位结构体 | Minor | Task 4 |
| M-5: `en` 前缀命名不符合 C# 规范 | Minor | Task 4 |
| M-6: 空的 `GamePlayEvents.cs` / `GlobalEvents.cs` | Minor | Task 4 |

---

## 文件修改清单

| 文件 | 操作 | 主要变更 |
|---|---|---|
| `Base/BaseSystem/EventSystem/EventBus.cs` | 修改 | 装箱修复、disposed守卫、Dispose对称、AsObservable、删死代码 |
| `Base/BaseSystem/EventSystem/EventDomain.cs` | 修改 | 命名空间、None守卫、调用EmitEnum、IDisposable |
| `Base/BaseSystem/EventSystem/EventSystem.cs` | 修改 | 命名空间、每域独立Bus、IsDisposed、Dispose所有域 |
| `TowerDefense/Core/GameFramework.cs` | 修改 | OnDestroy加EventSystem.Dispose() |
| `Base/BaseSystem/Events/UIEvents/UIEventCenter.cs` | 修改 | 命名空间、enUIEvent→UIEvent |
| `Base/BaseSystem/Events/GamePlayEvents/GamePlayEventCenter.cs` | 修改 | 命名空间、enGamePlayEvent→GamePlayEvent |
| `Base/BaseSystem/Events/GlobalEvents/GlobalEventCenter.cs` | 修改 | 命名空间、enGlobalEvent→GlobalEvent |
| `Base/BaseSystem/Events/UIEvents/UIEvents.cs` | 修改 | 删EventExample、加命名空间占位 |
| `Base/BaseSystem/Events/GamePlayEvents/GamePlayEvents.cs` | 修改 | 加命名空间占位 |
| `Base/BaseSystem/Events/GlobalEvents/GlobalEvents.cs` | 修改 | 加命名空间占位 |

所有路径相对于：`Purge Line/Assets/Scripts/`

---

## Chunk 1: 核心基础设施修复

### Task 1: 重写 EventBus.cs

**修复：** C-1（装箱）、C-2（disposed守卫）、I-1（Dispose对称）、I-2（Subject泄露）、I-4（命名空间）、M-1（死代码）

**文件：**
- Modify: `Purge Line/Assets/Scripts/Base/BaseSystem/EventSystem/EventBus.cs`

**关键设计决策：**
- 新增独立的 `EmitEnum<TEnum>` 泛型方法，用 `Unsafe.As<TEnum, int>` 替代 `Convert.ToInt32`，彻底消除装箱
- `Emit<T>` 只处理非枚举类型，删除运行时 `type.IsEnum` 分支
- `GetOrCreate<T>()` 返回 `subject.AsObservable()` 防止调用方反向转型取得 `OnNext` 权限
- `Dispose()` 对两个池统一使用 `OnCompleted()` + `Dispose()`
- `Emit`、`EmitEnum`、`GetOrCreate` 三个路径均加 `_disposed` 守卫

- [ ] **Step 1: 完整替换 EventBus.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using R3;

namespace PurgeLine.Events
{
    /// <summary>
    /// 底层事件总线。
    /// - 无参枚举事件：Subject&lt;Unit&gt;，key 为 (Type, int)，int 值通过 Unsafe.As 零装箱读取
    /// - 有参结构体事件：Subject&lt;T&gt;，key 为 typeof(T)
    /// </summary>
    internal sealed class EventBus : IDisposable
    {
        // 有参事件池：typeof(T) → Subject<T>（装箱存 object）
        private readonly Dictionary<Type, object> _typedPool = new();

        // 无参枚举事件池：(Type, int) → Subject<Unit>
        // key 用 (enumType, intValue) 避免不同 enum 相同数值碰撞
        private readonly Dictionary<(Type, int), Subject<Unit>> _enumPool = new();

        private bool _disposed;

        // ── 无参枚举事件 ────────────────────────────────────────

        /// <summary>
        /// 获取或创建枚举事件的 Observable。
        /// 使用 Unsafe.As 零装箱读取枚举整数值，假设枚举为 int 底层类型（项目约定）。
        /// </summary>
        public Observable<Unit> GetOrCreateEnum<TEnum>(TEnum enumValue)
            where TEnum : Enum
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));

            var intVal = Unsafe.As<TEnum, int>(ref enumValue);
            var key = (typeof(TEnum), intVal);
            if (!_enumPool.TryGetValue(key, out var subject))
            {
                subject = new Subject<Unit>();
                _enumPool[key] = subject;
            }
            return subject.AsObservable();
        }

        /// <summary>
        /// 派发无参枚举事件。无订阅者时不创建 Subject，节省内存。
        /// 使用 Unsafe.As 零装箱读取枚举整数值。
        /// </summary>
        public void EmitEnum<TEnum>(TEnum enumValue)
            where TEnum : Enum
        {
            if (_disposed) return;

            var intVal = Unsafe.As<TEnum, int>(ref enumValue);
            var key = (typeof(TEnum), intVal);
            if (_enumPool.TryGetValue(key, out var subject))
                subject.OnNext(Unit.Default);
        }

        // ── 有参事件 ────────────────────────────────────────────

        /// <summary>
        /// 获取或创建有参事件的 Observable。
        /// 返回 AsObservable() 封装，防止调用方反向转型为 Subject&lt;T&gt; 并调用 OnNext。
        /// </summary>
        public Observable<T> GetOrCreate<T>()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));

            var type = typeof(T);
            if (!_typedPool.TryGetValue(type, out var boxed))
            {
                var s = new Subject<T>();
                _typedPool[type] = s;
                return s.AsObservable();
            }
            return ((Subject<T>)boxed).AsObservable();
        }

        /// <summary>
        /// 派发有参事件。无订阅者时不创建 Subject，节省内存。
        /// 注意：此方法仅处理非枚举类型；枚举事件请使用 EmitEnum&lt;TEnum&gt;。
        /// </summary>
        public void Emit<T>(T eventData)
        {
            if (_disposed) return;

            if (_typedPool.TryGetValue(typeof(T), out var boxed))
                ((Subject<T>)boxed).OnNext(eventData);
        }

        // ── 生命周期 ─────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 统一策略：先发 OnCompleted 通知订阅者流结束，再 Dispose 释放内部资源
            foreach (var s in _enumPool.Values)
            {
                s.OnCompleted();
                ((IDisposable)s).Dispose();
            }
            foreach (var s in _typedPool.Values)
            {
                var subject = s as ISubject<object>; // 仅用于类型标识，实际走下方强转
                // Subject<T> 是 IDisposable，但 object 存储需要动态 dispatch
                // 通过接口强转触发 OnCompleted + Dispose
                if (s is IDisposable disposable)
                {
                    // Note: Subject<T> 内部在 Dispose 前会发 OnCompleted
                    disposable.Dispose();
                }
            }

            _enumPool.Clear();
            _typedPool.Clear();
        }
    }
}
```

> **注意：** `_typedPool` 中 `Subject<T>` 的 R3 实现会在 `Dispose()` 内部自动发送 `OnCompleted`，所以对 `_typedPool` 只需调用 `((IDisposable)s).Dispose()`。对 `_enumPool` 的 `Subject<Unit>` 保险起见显式调用 `OnCompleted()` 再 `Dispose()`。

- [ ] **Step 2: 验证编译** — 在 Unity 中打开项目，确认无编译错误（`Unsafe` 可用，R3 有 `AsObservable()`）

---

### Task 2: 重写 EventDomain.cs

**修复：** I-3（None守卫）、I-4（命名空间）、C-3（每域独立Bus）、修改 Dispatch 调用 EmitEnum

**文件：**
- Modify: `Purge Line/Assets/Scripts/Base/BaseSystem/EventSystem/EventDomain.cs`

**关键设计决策：**
- `EventDomain<TEnum>` 实现 `IDisposable`，在 `Dispose()` 中销毁自己持有的 `EventBus`
- `Dispatch(TEnum e)` 调用 `_bus.EmitEnum(e)`（而非原来的 `_bus.Emit(e)`），与装箱修复对接
- `None` 守卫：用 `Unsafe.As<TEnum, int>(ref e) == 0` 检查，零装箱

- [ ] **Step 3: 完整替换 EventDomain.cs**

```csharp
using System;
using System.Runtime.CompilerServices;
using R3;

namespace PurgeLine.Events
{
    /// <summary>
    /// 事件域：每个域持有独立的 EventBus，实现真正的域隔离。
    /// - 无参事件通过 TEnum 枚举约束限制在本域
    /// - 有参事件通过独立 Bus 保证不跨域触发
    /// </summary>
    public sealed class EventDomain<TEnum> : IDisposable
        where TEnum : Enum
    {
        private readonly EventBus _bus;
        private bool _disposed;

        internal EventDomain(EventBus bus) => _bus = bus;

        // ── 无参枚举事件 ─────────────────────────────────────────

        /// <summary>返回 Observable，支持 Rx 链式操作</summary>
        public Observable<Unit> OnEvent(TEnum e)
        {
            ValidateEvent(e);
            return _bus.GetOrCreateEnum(e);
        }

        /// <summary>命令式订阅，返回 IDisposable 用于取消</summary>
        public IDisposable AddListener(TEnum e, Action callback)
        {
            ValidateEvent(e);
            return _bus.GetOrCreateEnum(e).Subscribe(_ => callback());
        }

        /// <summary>派发无参事件</summary>
        public void Dispatch(TEnum e)
        {
            ValidateEvent(e);
            _bus.EmitEnum(e);
        }

        // ── 有参事件 ─────────────────────────────────────────────

        /// <summary>返回 Observable&lt;T&gt;，支持 Rx 链式操作</summary>
        public Observable<T> OnEvent<T>()
            => _bus.GetOrCreate<T>();

        /// <summary>命令式订阅，返回 IDisposable 用于取消</summary>
        public IDisposable AddListener<T>(Action<T> callback)
            => _bus.GetOrCreate<T>().Subscribe(callback);

        /// <summary>派发有参事件</summary>
        public void Dispatch<T>(T eventData)
            => _bus.Emit(eventData);

        // ── 生命周期 ──────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Dispose();
        }

        // ── 私有校验 ──────────────────────────────────────────────

        /// <summary>
        /// 拒绝使用默认枚举值（None = 0）派发或订阅事件。
        /// 使用 Unsafe.As 零装箱读取枚举 int 值，假设枚举为 int 底层类型（项目约定）。
        /// </summary>
        private static void ValidateEvent(TEnum e)
        {
            var intVal = Unsafe.As<TEnum, int>(ref e);
            if (intVal == 0)
                throw new ArgumentException(
                    $"[EventSystem] 不允许使用默认值 0（None）作为事件。请定义具体的事件枚举值。",
                    nameof(e));
        }
    }
}
```

- [ ] **Step 4: 验证编译** — 确认无报错

---

### Task 3: 重写 EventSystem.cs

**修复：** C-3（每域独立Bus）、I-4（命名空间）、M-2（IsDisposed）、C-4（前置准备）

**文件：**
- Modify: `Purge Line/Assets/Scripts/Base/BaseSystem/EventSystem/EventSystem.cs`

**关键设计决策：**
- 三个域各自构造独立的 `EventBus`（`new EventBus()`），保证有参事件域隔离
- `Dispose()` 依次 dispose 所有三个域

- [ ] **Step 5: 完整替换 EventSystem.cs**

```csharp
namespace PurgeLine.Events
{
    /// <summary>
    /// 全局事件系统入口。每个域持有独立的 EventBus，实现真正的域隔离。
    ///
    /// 用法：
    ///   EventSystem.Gameplay.Dispatch(GamePlayEvent.WaveCompleted);
    ///   EventSystem.Gameplay.Dispatch(new TowerPlacedEvent(...));
    ///   EventSystem.Gameplay.AddListener&lt;TowerPlacedEvent&gt;(OnTowerPlaced);
    ///
    /// 生命周期：由 GameFramework.OnDestroy() 调用 EventSystem.Dispose()。
    /// </summary>
    public static class EventSystem
    {
        public static readonly EventDomain<UIEvent>       UI       = new(new EventBus());
        public static readonly EventDomain<GamePlayEvent> Gameplay = new(new EventBus());
        public static readonly EventDomain<GlobalEvent>   Global   = new(new EventBus());

        public static bool IsDisposed { get; private set; }

        /// <summary>
        /// 游戏退出时清理所有域。由 GameFramework.OnDestroy() 调用，勿手动调用。
        /// </summary>
        public static void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            UI.Dispose();
            Gameplay.Dispose();
            Global.Dispose();
        }
    }
}
```

- [ ] **Step 6: 验证编译** — 确认无报错

---

## Chunk 2: 枚举类型重命名 + 清理死代码

### Task 4: 枚举文件重命名 + 命名空间 + 清理占位代码

**修复：** M-5（en前缀）、I-4（命名空间）、M-3（EventExample）、M-6（空文件）

**文件：**
- Modify: `Base/BaseSystem/Events/UIEvents/UIEventCenter.cs`
- Modify: `Base/BaseSystem/Events/GamePlayEvents/GamePlayEventCenter.cs`
- Modify: `Base/BaseSystem/Events/GlobalEvents/GlobalEventCenter.cs`
- Modify: `Base/BaseSystem/Events/UIEvents/UIEvents.cs`
- Modify: `Base/BaseSystem/Events/GamePlayEvents/GamePlayEvents.cs`
- Modify: `Base/BaseSystem/Events/GlobalEvents/GlobalEvents.cs`

**关键设计决策：**
- 枚举命名从 `enXxxEvent` 改为 `XxxEvent`（符合 C# 命名规范 PascalCase）
- `EventSystem.cs` 中的引用已在 Task 3 中同步更新（`UIEvent`、`GamePlayEvent`、`GlobalEvent`）
- `EventExample` 删除，UIEvents.cs 保留文件但仅含命名空间声明和注释
- 空文件加命名空间声明（不删除文件，避免 Unity .meta 孤立问题）

- [ ] **Step 7: 更新 UIEventCenter.cs**

```csharp
namespace PurgeLine.Events
{
    /// <summary>UI 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum UIEvent
    {
        None = 0,
        // 在此添加 UI 事件，例如：
        // OpenPanel,
        // ClosePanel,
        Max,
    }
}
```

- [ ] **Step 8: 更新 GamePlayEventCenter.cs**

```csharp
namespace PurgeLine.Events
{
    /// <summary>Gameplay 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum GamePlayEvent
    {
        None = 0,
        // 在此添加 Gameplay 事件，例如：
        // WaveStarted,
        // WaveCompleted,
        // TowerPlaced,
        Max,
    }
}
```

- [ ] **Step 9: 更新 GlobalEventCenter.cs**

```csharp
namespace PurgeLine.Events
{
    /// <summary>Global 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum GlobalEvent
    {
        None = 0,
        // 在此添加全局事件，例如：
        // GamePaused,
        // GameResumed,
        // ApplicationQuit,
        Max,
    }
}
```

- [ ] **Step 10: 更新 UIEvents.cs（删除 EventExample，保留命名空间）**

```csharp
// UIEvents.cs — UI 域有参事件数据结构定义区
// 在此定义 UI 域使用的 struct 事件，例如：
//
// public readonly struct OpenPanelEvent
// {
//     public readonly string PanelId;
//     public OpenPanelEvent(string panelId) => PanelId = panelId;
// }

namespace PurgeLine.Events { }
```

- [ ] **Step 11: 更新 GamePlayEvents.cs（空文件加命名空间）**

```csharp
// GamePlayEvents.cs — Gameplay 域有参事件数据结构定义区
// 在此定义 Gameplay 域使用的 struct 事件，例如：
//
// public readonly struct TowerPlacedEvent
// {
//     public readonly int GridX;
//     public readonly int GridY;
//     public TowerPlacedEvent(int x, int y) { GridX = x; GridY = y; }
// }

namespace PurgeLine.Events { }
```

- [ ] **Step 12: 更新 GlobalEvents.cs（空文件加命名空间）**

```csharp
// GlobalEvents.cs — Global 域有参事件数据结构定义区
// 在此定义全局域使用的 struct 事件，例如：
//
// public readonly struct GameStateChangedEvent
// {
//     public readonly int NewState;
//     public GameStateChangedEvent(int state) => NewState = state;
// }

namespace PurgeLine.Events { }
```

- [ ] **Step 13: 验证编译** — Unity 控制台无报错

---

## Chunk 3: 生命周期接入 GameFramework

### Task 5: GameFramework.OnDestroy() 接入 EventSystem.Dispose()

**修复：** C-4（EventSystem.Dispose()自动调用）

**文件：**
- Modify: `Purge Line/Assets/Scripts/TowerDefense/Core/GameFramework.cs`

**关键设计决策：**
- 在 `OnDestroy()` 中于 `GameLogger.Dispose()` **之前**调用 `EventSystem.Dispose()`，
  确保日志系统在事件系统销毁的日志输出之后才关闭
- 添加 `using PurgeLine.Events;`

- [ ] **Step 14: 在 GameFramework.cs 顶部添加 using**

在现有 `using` 块末尾增加：
```csharp
using PurgeLine.Events;
```

- [ ] **Step 15: 修改 GameFramework.OnDestroy()**

将现有的：
```csharp
private void OnDestroy()
{
    if (Instance != this) return;

    _logger.LogInformation("Framework destroyed");
    GameLogger.Dispose();
    Instance = null;
}
```

改为：
```csharp
private void OnDestroy()
{
    if (Instance != this) return;

    _logger.LogInformation("Framework destroying — shutting down event system...");
    EventSystem.Dispose();

    _logger.LogInformation("Framework destroyed");
    GameLogger.Dispose();
    Instance = null;
}
```

- [ ] **Step 16: 验证编译 + 手动测试**

  1. 在 Unity 编辑器中点击 Play，确认无异常
  2. 停止 Play，确认控制台有 "Framework destroying — shutting down event system..." 日志
  3. 再次 Play，确认上次的订阅不会复活（验证 C-4 修复有效）

---

## 完成校验清单

- [ ] 无编译错误
- [ ] `EventSystem.UI`、`EventSystem.Gameplay`、`EventSystem.Global` 三个域各持独立 `EventBus`
- [ ] `EventSystem.UI.Dispatch(new SomeStruct())` 不会触发 `EventSystem.Gameplay` 的订阅者（C-3 验证）
- [ ] `EventSystem.Gameplay.Dispatch(GamePlayEvent.None)` 抛出 `ArgumentException`（I-3 验证）
- [ ] Play Mode 退出后控制台出现 EventSystem.Dispose 日志（C-4 验证）
- [ ] 枚举名称全部为 `UIEvent`、`GamePlayEvent`、`GlobalEvent`，无 `en` 前缀（M-5 验证）
- [ ] 全局命名空间无残余 `public` 事件系统类型（I-4 验证）

---

## 备注：未做的 Trade-off

- **I-5（AddListener lambda 闭包）**：`_ => callback()` 每次订阅产生一次 closure 分配，发生在订阅建立时而非每帧，运行时无 GC 压力，保持现状。
- **M-2（IsDisposed）**：已在 Task 3 `EventSystem` 中加入 `IsDisposed` 属性。
- **`Max` 枚举值防护**：`Max` 用于边界检查，代码约定禁止使用，不做运行时校验（反射代价过高）。
