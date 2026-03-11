# 🏰 Purge Line — 2D Tower Defense Grid System Architecture

> **版本**: 1.0  
> **日期**: 2026-03-11  
> **架构师**: Senior Client Architect  
> **引擎**: Unity 2022.3 LTS + Entities 1.3.15  

---

## 1. 总体架构设计

### 1.1 模块划分

```
┌─────────────────────────────────────────────────────────────────┐
│                        Editor Tools Layer                       │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────┐  │
│  │ GridEditorWindow │  │ GridSceneOverlay │  │ LevelExporter │  │
│  └────────┬────────┘  └────────┬─────────┘  └───────┬───────┘  │
│           └────────────────────┼──────────────────────┘         │
│                                │ ScriptableObject I/O           │
├────────────────────────────────┼────────────────────────────────┤
│                        Bridge Layer                             │
│  ┌─────────────────────────────┴─────────────────────────────┐  │
│  │               GridBridgeSystem (Managed)                  │  │
│  │  - 注册到 SystemManager                                   │  │
│  │  - 持有 ECS World 引用                                    │  │
│  │  - R3 事件发布 (MapLoaded, CellChanged)                   │  │
│  │  - 提供 Managed API 查询接口                              │  │
│  └─────────────────────────────┬─────────────────────────────┘  │
├────────────────────────────────┼────────────────────────────────┤
│                        ECS Runtime Layer                        │
│  ┌──────────────┐  ┌──────────┴───────┐  ┌──────────────────┐  │
│  │  Components   │  │    Systems       │  │   Utilities      │  │
│  │              │  │                  │  │                  │  │
│  │ GridMapData  │  │ GridSpawnSystem  │  │ GridMath         │  │
│  │ CellType     │  │ GridModSystem    │  │ (Burst-compiled) │  │
│  │ GridCoord    │  │ GridRenderSystem │  │                  │  │
│  │ OccupantRef  │  │                  │  │                  │  │
│  └──────────────┘  └──────────────────┘  └──────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                        Data Layer                               │
│  ┌──────────────────┐  ┌──────────────────────────────────────┐ │
│  │ LevelConfig      │  │ LevelConfigLoader                   │ │
│  │ (MemoryPack)     │  │ (UniTask async / sync Editor path)  │ │
│  └──────────────────┘  └──────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 依赖关系

```
GridEditorWindow ──→ LevelConfigAsset ──→ LevelConfig (MemoryPack)
       │                                        │
       ▼                                        ▼
GridSceneOverlay                     LevelConfigLoader (UniTask)
                                            │
GridBridgeSystem ──→ SystemManager          │
       │              (existing)            │
       ▼                                    ▼
  ECS World ←── GridSpawnSystem ←── LevelConfig binary data
       │
       ├── GridMapData (Singleton IComponentData)
       ├── GridModificationSystem
       └── GridRenderSystem
```

### 1.3 数据流

```
[Editor] 可视化编辑 → LevelConfigAsset (ScriptableObject)
                        → Export → .bytes (MemoryPack binary)
                                    → Addressables / Resources

[Runtime] 
  GameBootstrapper.Awake()
    → GameFramework.Initialize()
      → SystemManager.Register<GridBridgeSystem>()
        → GridBridgeSystem.OnStart()
          → LevelConfigLoader.LoadAsync("level_01")
            → MemoryPack.Deserialize<LevelConfig>()
              → GridSpawnSystem creates GridMapData singleton
                → GridRenderSystem reads GridMapData, batched render
                → GridBridgeSystem publishes R3 MapLoaded event
```

---

## 2. 核心组件/系统定义

### 2.1 ECS 组件结构

| 组件 | 类型 | 职责 | 存储 |
|------|------|------|------|
| `GridMapData` | `IComponentData` | 地图元数据 (Width, Height, CellSize, Origin) + BlobAssetReference | Singleton Entity |
| `GridCellState` | `IBufferElementData` | 每格子运行时状态 (占据者Entity等) | Singleton Entity Buffer |
| `CellType` | `[Flags] byte` enum | 格子基础类型标记 | BlobArray 内 |
| `GridCoord` | `IComponentData` | 挂载在炮塔/建筑实体上的格子坐标 | Per-Entity |
| `OccupantRef` | `IComponentData` | 格子上占据者的 Entity 引用 | Per-Entity (Tower) |
| `GridSpawnRequest` | `IComponentData` | 触发地图生成的一次性请求 | Singleton, consumed |
| `GridDirtyTag` | `IComponentData` (tag) | 标记地图数据已被修改，需要刷新渲染 | Singleton |

### 2.2 系统职责

| 系统 | 类型 | 更新组 | 职责 |
|------|------|--------|------|
| `GridSpawnSystem` | `ISystem` | `InitializationSystemGroup` | 消费 `GridSpawnRequest`，创建 BlobAsset + singleton |
| `GridModificationSystem` | `ISystem` | `SimulationSystemGroup` | 处理格子状态变更（炮塔放置/移除） |
| `GridRenderSystem` | `ISystem` | `PresentationSystemGroup` | 批量渲染格子瓦片 |
| `GridBridgeSystem` | `ISystem` (Managed框架) | `SystemManager` | 桥接 ECS ↔ Managed 世界 |

### 2.3 Job 逻辑

```csharp
// GridSpawnSystem 中的 BlobAsset 构建 — 主线程一次性构建
// GridRenderSystem 中的 Matrix4x4 填充 — IJobParallelFor + Burst
// GridMath 工具函数 — 全部 [BurstCompile] 静态方法
```

---

## 3. 文件结构

```
Assets/Scripts/TowerDefense/
├── TowerDefense.asmdef                    # Runtime Assembly
├── Components/
│   ├── CellType.cs                        # [Flags] byte 枚举
│   ├── GridMapData.cs                     # Singleton IComponentData
│   ├── GridCellState.cs                   # IBufferElementData
│   ├── GridCoord.cs                       # IComponentData
│   ├── GridSpawnRequest.cs                # 一次性请求组件
│   └── GridDirtyTag.cs                    # Tag 组件
├── Systems/
│   ├── GridSpawnSystem.cs                 # 地图实体生成
│   ├── GridModificationSystem.cs          # 格子状态修改
│   └── GridRenderSystem.cs                # 批量渲染
├── Utilities/
│   └── GridMath.cs                        # Burst 编译数学工具
├── Data/
│   ├── LevelConfig.cs                     # MemoryPack 序列化配置
│   └── LevelConfigLoader.cs              # 加载器
├── Bridge/
│   └── GridBridgeSystem.cs               # Managed 桥接层
├── Editor/
│   ├── TowerDefense.Editor.asmdef        # Editor Assembly
│   ├── GridEditorWindow.cs               # 编辑器主窗口
│   ├── GridSceneOverlay.cs               # Scene 视图叠加层
│   ├── LevelConfigAsset.cs               # ScriptableObject 包装
│   └── LevelConfigAssetEditor.cs         # 自定义 Inspector
├── Tests/
│   ├── TowerDefense.Tests.asmdef         # Test Assembly
│   ├── GridMathTests.cs                  # 数学工具测试
│   ├── LevelConfigSerializationTests.cs  # 序列化往返测试
│   ├── GridSpawnSystemTests.cs           # ECS 系统集成测试
│   └── GridPerformanceTests.cs           # 性能基准测试
└── Documentation/
    ├── Architecture.md                    # 本文档
    └── UserGuide.md                       # 使用指南
```

---

## 4. 编辑器工具设计

### 4.1 GridEditorWindow

- **入口**: `Window > Tower Defense > Grid Editor`
- **功能**:
  - 新建/加载/保存关卡配置
  - 网格尺寸设置 (Width × Height)
  - 画笔模式：选择 CellType 后点击/拖拽绘制
  - 橡皮模式：擦除为 None
  - 填充模式：批量填充矩形区域
  - Undo/Redo 支持（通过 `Undo.RecordObject`）
  - 快捷键绑定

### 4.2 GridSceneOverlay

- Scene 视图中叠加透明网格
- 不同 CellType 显示不同颜色:
  - Solid = 深灰 (0.3, 0.3, 0.3, 0.5)
  - Walkable = 绿色 (0.2, 0.8, 0.2, 0.3)
  - Placeable = 蓝色 (0.2, 0.4, 0.9, 0.3)
  - WalkableAndPlaceable = 青色 (0.2, 0.8, 0.8, 0.3)

### 4.3 数据序列化方案

```
ScriptableObject (LevelConfigAsset)
    ↕ Inspector 编辑
    ↓ Export 按钮
MemoryPack Binary (.bytes)
    → Assets/Data/Levels/{levelId}.bytes
    → Addressables Group "Levels"
```

---

## 5. 数据持久化方案

### 5.1 配置格式

```csharp
[MemoryPackable]
public partial class LevelConfig
{
    public string LevelId;           // 关卡唯一标识
    public int Version;              // 配置版本号
    public int Width;                // 地图宽度（格子数）
    public int Height;               // 地图高度（格子数）
    public float CellSize;           // 单个格子世界尺寸
    public float OriginX, OriginY;   // 地图原点世界坐标
    public byte[] Cells;             // 扁平化格子数组 [y * Width + x]
    public string[] SpawnPoints;     // 敌人出生点（预留）
    public string[] GoalPoints;      // 目标点（预留）
}
```

### 5.2 存储路径

| 环境 | 路径 | 说明 |
|------|------|------|
| Editor | `Assets/Data/Levels/{levelId}.bytes` | 开发期资产 |
| Runtime | Addressables / Resources | 加载路径 |
| 热更新 | `{Application.persistentDataPath}/Levels/` | 覆盖 bundle 内配置 |

### 5.3 加载逻辑

```
1. 检查 persistentDataPath (热更新覆盖)
2. 回退到 Addressables/Resources
3. MemoryPack 反序列化
4. 创建 GridSpawnRequest ECS entity
5. GridSpawnSystem 消费请求，构建 BlobAsset
```

---

## 6. 美术集成方案

### 6.1 资源规范

- 格子瓦片：Sprite Atlas，每种 CellType 对应一个 Sprite
- 推荐尺寸：64×64 或 128×128 像素
- 格式：PNG，使用 Sprite Atlas 打包减少 draw call

### 6.2 渲染流程

```
GridRenderSystem (PresentationSystemGroup)
  1. 读取 GridMapData singleton (BlobAsset)
  2. IJobParallelFor 填充 NativeArray<Matrix4x4>（每种类型分组）
  3. 主线程调用 Graphics.RenderMeshInstanced()
     - 每种 CellType 一个 draw call
     - Quad Mesh + URP Sprite-Lit Material
     - MaterialPropertyBlock 设置不同纹理/颜色
```

### 6.3 合批优化

- 200×200 = 40,000 格子
- 假设 4 种类型，每种约 10,000 格子
- `Graphics.RenderMeshInstanced` 单次最多 1023 实例
- 每种类型约 10 个 batch = 总共 ~40 draw calls
- GPU Instancing 使 40k 格子渲染无压力

---

## 7. 性能测试验证标准

详见 [PerformanceTestPlan.md](./PerformanceTestPlan.md)

### 快速指标

| 指标 | 目标 | 测试方法 |
|------|------|----------|
| 200×200 地图生成 | < 100ms | `Stopwatch` + Performance Test |
| 格子坐标查询 | < 1ms (批量10000次) | `[Test]` + `Measure.Method()` |
| 地图渲染帧耗 | < 2ms | Profiler Marker |
| MemoryPack 序列化往返 | < 10ms | `[Test]` |
| 内存占用 (200×200) | < 200KB grid data | Memory Profiler |
| GC Allocation (per frame) | 0 bytes | Profiler |

