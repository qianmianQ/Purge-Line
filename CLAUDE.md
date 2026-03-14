# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Purge Line** 是一个 Unity 2022.3.62f3c1 + URP 的 2D 塔防游戏，使用 ECS (Unity Entities 1.3.15) 处理核心逻辑，自定义 DependencyManager 框架管理托管系统生命周期。

- **Unity 项目路径**: `Purge Line/`（不是根目录）
- **API**: .NET Standard 2.1

## 启动流程

```
GameBootstrapper.Awake()
  → GameFramework.Awake()
    → ZLogger 初始化
    → EventManager.Init(UIEvent, GamePlayEvent, GlobalEvent)
    → 创建 [DependencyManager] GameObject
    → GameFramework.Initialize()  // 注册所有托管系统
  → GameFramework.Start()
    → EcsVisualBridgeSystem.InitEntitiesPools(...)
    → Invoke(StartGame, 3f)
      → GridBridgeSystem.LoadLevel("level_01")
```

## 架构层次

```
Editor Tools Layer    GridEditorWindow / GridSceneOverlay
      ↓
Bridge Layer          托管系统，注册到 DependencyManager
                      GridBridgeSystem / CombatBridgeSystem / TowerPlacementSystem / EcsVisualBridgeSystem
      ↓
ECS Runtime Layer     Unity Entities 系统（ISystem，Burst 编译）
                      GridSpawnSystem / GridModificationSystem / GridRenderSystem / 战斗系统
      ↓
Data Layer            LevelConfig (MemoryPack .bytes) / SharedLevelDataStore
```

## DependencyManager（核心框架）

位于 `UnitySystemArchitecture/DependencyManager.cs`，命名空间 `UnityDependencyInjection`。

**模块接口**：
- `IInitializable` — `OnInit()` / `OnDispose()`（Register 时立即调用）
- `IStartable` — `OnStart()`（第一帧 Update 时调用，用于延迟获取依赖）
- `ITickable` / `IFixedTickable` / `ILateTickable` — 各帧回调

**关键 API**：
```csharp
DependencyManager.Instance.Register(new MySystem());  // 注册并立即调用 OnInit
DependencyManager.Instance.Get<MySystem>();             // 获取系统
```

**注意**：依赖应在 `OnStart()` 中获取（非 `OnInit()`），因为 OnInit 时其他系统可能未注册。

## 事件系统

三域隔离（UI、Gameplay、Global），底层使用 R3：

```csharp
// 无参枚举事件
EventManager.Gameplay.Dispatch(GamePlayEvent.WaveCompleted);
EventManager.Gameplay.AddListener(GamePlayEvent.WaveCompleted, OnWaveCompleted);

// 有参结构体事件
EventManager.Gameplay.Dispatch(new GridCellChangedEvent { ... });
EventManager.Gameplay.AddListener<GridCellChangedEvent>(OnCellChanged);

// Rx 链式操作
EventManager.UI.OnEvent<SomeEvent>().Where(...).Subscribe(...);
```

**枚举约定**：所有枚举必须为 `int` 底层类型，值 `0`（None）为保留值不可用于派发。

## ECS 层设计

**地图相关 ECS 组件**（`TowerDefense/ECS/Components/`）：
- `GridMapData` — Singleton，地图元数据 + BlobAssetReference
- `GridCellState` — IBufferElementData，每格运行时状态
- `GridSpawnRequest` — 一次性请求，由 GridBridgeSystem 创建，GridSpawnSystem 消费
- `FlowFieldData` / `FlowFieldAgent` — 流场寻路数据

**ECS 系统更新组**：
- `InitializationSystemGroup`: GridSpawnSystem
- `SimulationSystemGroup`: GridModificationSystem、FlowFieldBakeSystem、战斗系统
- `PresentationSystemGroup`: GridRenderSystem、VisualBridgeSystem

## 关卡数据管线

```
Editor: LevelConfigAsset (ScriptableObject) → [GridEditorWindow 导出] → {levelId}.bytes (MemoryPack)
Runtime: GridBridgeSystem.LoadLevel(id)
           → LevelConfigLoader.LoadFromResources(id)
           → SharedLevelDataStore.Store(config)
           → 创建 GridSpawnRequest ECS Entity
           → GridSpawnSystem 消费请求，构建 BlobAsset
```

关卡文件路径：`Assets/Data/Levels/{levelId}.bytes`

## 关键文件路径

- **DependencyManager**: `Assets/Scripts/UnitySystemArchitecture/DependencyManager.cs`
- **GameFramework**: `Assets/Scripts/TowerDefense/Core/GameFramework.cs`
- **LevelConfig**: `Assets/Scripts/TowerDefense/Data/LevelConfig.cs`
- **GridBridgeSystem**: `Assets/Scripts/TowerDefense/ECS/Bridge/GridBridgeSystem.cs`
- **EventManager**: `Assets/Scripts/Base/BaseSystem/EventSystem/EventManager.cs`
