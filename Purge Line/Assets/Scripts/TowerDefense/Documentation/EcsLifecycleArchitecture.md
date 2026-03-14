# ECS Lifecycle 架构设计（工业级封装）

## 目标
- ECS 不再自动启动；仅在玩家选择关卡并点击开始后启动。
- 统一接管 `World` 与 `System` 全生命周期：创建、注册、更新、暂停、销毁。
- 外部只通过 API 访问，避免业务代码直接操作 `World.DefaultGameObjectInjectionWorld`。
- 接入项目日志系统、快照、调试观测与编辑器可视化控制。

## 设计总览

### 核心组件
- `ManualEcsBootstrap`：实现 `ICustomBootstrap`，阻止 Entities 默认自动创建 World。
- `IEcsLifecycleService` / `EcsLifecycleService`：
  - 统一管理手动 World。
  - 使用 `DefaultWorldInitialization.GetAllSystems` 自动注册全部系统。
  - 手动驱动 `Initialization/Simulation/Presentation` 三大组。
  - 提供状态机、快照、性能指标。
- `IEcsWorldAccessor`：桥接系统只读访问当前受管 World。
- `EcsLifecycleWindow`（Editor）：可视化启停、暂停、恢复、加载关卡、快照查看。

### 状态机
- `Uninitialized -> Ready -> Starting -> Running -> Paused -> Stopping -> Stopped`
- 异常进入 `Failed`，服务销毁进入 `Disposed`。
- 每次状态迁移自动记录快照（ring buffer）。

## 对外 API

### `IEcsLifecycleService`
- `StartWorld()`：创建并注册系统，进入 `Running`。
- `StopWorld()`：销毁 World，释放系统与资源。
- `PauseWorld()` / `ResumeWorld()`：控制更新节奏。
- `CaptureSnapshot(note)`：主动采样运行态。
- `GetSnapshots()`：读取最近快照。
- `RuntimeStatistics`：系统数、实体数、帧耗时、平均耗时、最近错误。

### `GameFramework`
- `StartGameSession(levelId)`：
  1. 启动 ECS World
  2. 通过 `IGridBridgeSystem.LoadLevel(levelId)` 发起关卡加载
  3. 失败时自动回滚并停止 World
- `StopGameSession()`：停止当前会话

## 性能与内存权衡
- 自动系统注册只在 `StartWorld` 执行一次，避免每帧反射扫描。
- 帧时序统计复用 `Stopwatch`，避免额外分配。
- 快照使用 ring buffer（默认 64 条），防止内存无限增长。
- 实体数采样每 30 帧一次，降低 `UniversalQuery.CalculateEntityCount` 开销。

## 日志与可观测性
- 使用 `GameLogger` 分类 `EcsLifecycleService`。
- 记录关键事件：启动、停止、状态迁移、快照、异常。
- 快照包含：
  - 状态
  - World 名称
  - 实体数
  - 注册系统数
  - GC 托管内存
  - 最近一帧 Init/Sim/Pres 耗时

## 桥接层改造策略
- `GridBridgeSystem`、`CombatBridgeSystem` 改为注入 `IEcsWorldAccessor`。
- 所有 ECS 调用前执行 `TryGetWorld`，保证在手动启动前不会误操作。
- 保留兼容：`EcsLifecycleService` 启动时设置 `World.DefaultGameObjectInjectionWorld` 指向受管 World（逐步迁移期）。

## 编辑器工具（MVP）
- 菜单：`PurgeLine/ECS Lifecycle Monitor`
- 功能：
  - Start / Pause / Resume / Stop
  - Start Session + Load Level
  - Capture Snapshot
  - 运行指标看板 + 快照时间线

## 兼容性与风险
- 若存在外部代码依赖默认自动 World，需改造为走 `IEcsWorldAccessor`。
- 手动驱动时序后，必须确保 `Tick()` 每帧执行（已通过 VContainer `ITickable` 接管）。
- 若某些第三方系统依赖 PlayerLoop 自动挂载，需在后续阶段评估是否引入可选模式。

## 后续增强建议
- 增加系统级 TopN profiler（分系统耗时而非仅分组耗时）。
- 支持可恢复快照（关键 singleton + buffer 差量回放）。
- 增加运行时健康检查（系统缺失、关键 singleton 缺失报警）。

