# ECS Lifecycle 改造任务报告

## 任务目标
- 从“ECS 自动启动”迁移为“关卡选择后手动启动”。
- 统一接管 World/System 生命周期与时序。
- 引入日志、快照、编辑器观测能力。

## 已完成项
- 新增生命周期接口与数据模型：
  - `Assets/Scripts/TowerDefense/ECS/Lifecycle/EcsLifecycleContracts.cs`
- 新增生命周期服务：
  - `Assets/Scripts/TowerDefense/ECS/Lifecycle/EcsLifecycleService.cs`
- 禁用默认自动 World：
  - `Assets/Scripts/TowerDefense/ECS/Lifecycle/ManualEcsBootstrap.cs`
- DI 接入：
  - `Assets/Scripts/TowerDefense/Core/DI/GameLifetimeScope.cs`
- 桥接层改造：
  - `Assets/Scripts/TowerDefense/ECS/Bridge/GridBridgeSystem.cs`
  - `Assets/Scripts/TowerDefense/ECS/Bridge/CombatBridgeSystem.cs`
- 游戏入口改造（手动会话 API）：
  - `Assets/Scripts/TowerDefense/Core/GameFramework.cs`
- 编辑器可视化监控：
  - `Assets/Scripts/TowerDefense/Editor/EcsLifecycleWindow.cs`
- 架构/使用文档：
  - `Assets/Scripts/TowerDefense/Documentation/EcsLifecycleArchitecture.md`
  - `Assets/Scripts/TowerDefense/Documentation/EcsLifecycleUsage.md`

## 关键技术决策
- 使用 `ICustomBootstrap` 阻止 ECS 默认世界自动创建。
- 用 `DefaultWorldInitialization.GetAllSystems` 自动注册系统，减少手工维护成本。
- 通过 VContainer `ITickable` 手动驱动三大系统组，保证启停可控。
- 快照采用 ring buffer，避免长时间运行内存增长。

## 性能与内存结果（设计预期）
- 启动前无 ECS 系统更新成本。
- 运行中仅有轻量统计开销（分组耗时 + 定频实体计数）。
- 快照固定容量，内存可控。

## 风险与后续计划
- 少量遗留代码仍可能直接读取 `World.DefaultGameObjectInjectionWorld`，建议逐步统一到 `IEcsWorldAccessor`。
- 下一阶段增加系统级耗时 TopN 和异常恢复策略。
- 建议补充 PlayMode 自动化测试覆盖：Start/Stop/Pause/Resume/重复启停。

