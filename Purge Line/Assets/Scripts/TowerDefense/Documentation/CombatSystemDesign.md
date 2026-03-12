# 🏗️ 2D 俯视角塔防核心战斗系统 — 架构设计文档

## 1. 架构总览

```
┌────────────────────────────────────────────────────────────────────┐
│                      Managed World (MonoBehaviour)                  │
│  ┌──────────────┐  ┌──────────────────┐  ┌─────────────────────┐  │
│  │ GameFramework │  │ GridBridgeSystem │  │ TowerPlacementInput │  │
│  │  (入口/DI)    │  │  (桥接/查询API)  │  │  (Input System)     │  │
│  └──────────────┘  └──────────────────┘  └─────────────────────┘  │
│           ↕ DependencyManager                    ↕ EventManager    │
├────────────────────────────────────────────────────────────────────┤
│                      ECS World (DOTS)                              │
│  ┌───────────────────── InitializationSystemGroup ──────────────┐ │
│  │  GridSpawnSystem → FlowFieldBakeSystem → EnemySpawnSystem    │ │
│  └──────────────────────────────────────────────────────────────┘ │
│  ┌───────────────────── SimulationSystemGroup ──────────────────┐ │
│  │  FlowFieldMovementSystem                                     │ │
│  │  → TowerTargetingSystem  (炮塔搜敌)                          │ │
│  │  → TowerAttackSystem     (发射子弹)                          │ │
│  │  → BulletMovementSystem  (子弹飞行)                          │ │
│  │  → BulletHitSystem       (命中检测+伤害)                     │ │
│  │  → HealthDeathSystem     (死亡清理)                          │ │
│  │  → EnemyGoalReachedSystem(到达终点)                          │ │
│  │  → EntityCleanupSystem   (对象池回收)                        │ │
│  └──────────────────────────────────────────────────────────────┘ │
│  ┌───────────────────── PresentationSystemGroup ────────────────┐ │
│  │  GridRenderSystem (已有)                                      │ │
│  └──────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────┘
```

## 2. 模块划分

### 2.1 Components (ECS 组件)

| Component | 类型 | 说明 |
|-----------|------|------|
| `TowerData` | IComponentData | 炮塔静态属性：攻击范围/间隔/伤害/等级 |
| `TowerState` | IComponentData | 炮塔运行时状态：攻击计时器/当前目标 |
| `TowerTag` | IComponentData | 炮塔标记 (Tag) |
| `EnemyData` | IComponentData | 敌人属性：最大生命/移动速度 |
| `EnemyTag` | IComponentData | 敌人标记 (Tag) |
| `HealthData` | IComponentData | 生命值组件：当前HP/最大HP |
| `BulletData` | IComponentData | 子弹属性：伤害/速度/最大飞行距离 |
| `BulletState` | IComponentData | 子弹运行时状态：目标Entity/起始位置/已飞行距离 |
| `BulletTag` | IComponentData | 子弹标记 (Tag) |
| `DeadTag` | IComponentData | 死亡标记 (Tag) |
| `DestroyTag` | IComponentData | 销毁标记 (Tag) |
| `DisabledTag` | IComponentData | 对象池已回收标记 |
| `SpawnPointData` | IBufferElementData | 出生点数据 |
| `EnemySpawnTimer` | IComponentData | 敌人生成计时器 (Singleton) |

### 2.2 Systems (ECS 系统)

按执行顺序：

| System | Group | Burst | 说明 |
|--------|-------|-------|------|
| `EnemySpawnSystem` | Initialization | ✗(managed) | 按配置频率在出生点生成敌人 |
| `FlowFieldMovementSystem` | Simulation | ✓ | 已有-驱动敌人流场移动 |
| `TowerTargetingSystem` | Simulation | ✓ | 炮塔搜索范围内最近敌人 |
| `TowerAttackSystem` | Simulation | ✓ | 按攻击间隔创建子弹ECB |
| `BulletMovementSystem` | Simulation | ✓ | 子弹朝目标飞行+追踪 |
| `BulletHitSystem` | Simulation | ✓ | 命中检测→扣血→标记销毁 |
| `HealthDeathSystem` | Simulation | ✓ | HP≤0→添加DeadTag |
| `EnemyGoalReachedSystem` | Simulation | ✓ | 检测ReachedGoal→触发终点逻辑 |
| `EntityCleanupSystem` | Simulation | ✗(ECB) | 处理DestroyTag实体的销毁/回收 |

### 2.3 Managed Systems (托管层)

| System | 注册方式 | 说明 |
|--------|----------|------|
| `TowerPlacementSystem` | DependencyManager | 处理输入、虚影显示、放置逻辑 |
| `CombatBridgeSystem` | DependencyManager | 战斗参数桥接/配置管理 |

## 3. Component 定义详解

### 3.1 TowerData
```csharp
public struct TowerData : IComponentData
{
    public float AttackRange;       // 攻击范围 (世界单位)
    public float AttackInterval;    // 攻击间隔 (秒)
    public float BulletSpeed;       // 子弹速度 (世界单位/秒)
    public int   Damage;            // 伤害值
    public int   Level;             // 当前等级
    public int   TowerTypeId;       // 炮塔类型ID (扩展用)
}
```

### 3.2 EnemyData
```csharp
public struct EnemyData : IComponentData
{
    public int   EnemyTypeId;       // 敌人类型ID (扩展用)
    public float BaseSpeed;         // 基础移动速度
    public int   RewardGold;        // 击杀奖励
}
```

### 3.3 BulletData + BulletState
```csharp
public struct BulletData : IComponentData
{
    public int   Damage;            // 伤害
    public float Speed;             // 飞行速度
    public float MaxRange;          // 最大飞行距离
    public int   BulletTypeId;      // 子弹类型ID (扩展用)
}

public struct BulletState : IComponentData
{
    public Entity TargetEntity;     // 追踪目标
    public float3 StartPosition;    // 起始位置
    public float  DistanceTraveled; // 已飞行距离
    public bool   HasHit;           // 是否已命中（防重复伤害）
}
```

## 4. 核心流程时序图

### 4.1 炮塔放置流程
```
Player ──[P键]──→ TowerPlacementSystem.EnterPlacementMode()
  │                    │
  │                    ├── 创建虚影 GameObject (半透明)
  │                    ├── 创建提示物体 (ui tip.prefab)
  │                    │
  ├──[鼠标移动]───→ Update:
  │                    ├── 获取鼠标世界坐标
  │                    ├── GridMath.WorldToGrid → 吸附格子
  │                    ├── GridBridgeSystem.CanPlaceAt() → 检查
  │                    │   ├── ✓可放置: 虚影白色
  │                    │   └── ✗不可放置: 虚影红色 + 日志
  │                    └── 更新虚影+提示物体位置
  │
  ├──[左键点击]───→ CanPlaceAt?
  │                    ├── ✓: GridBridgeSystem.PlaceTower()
  │                    │   ├── ECS Entity 创建 (Tower Entity.prefab)
  │                    │   ├── 添加 TowerData/TowerState/GridCoord
  │                    │   ├── GridCellState 标记占据
  │                    │   └── 退出放置模式
  │                    └── ✗: 日志提示
  │
  └──[P键/ESC]───→ ExitPlacementMode() → 销毁虚影+提示物体
```

### 4.2 战斗循环时序
```
每帧 SimulationSystemGroup:
  │
  ├── FlowFieldMovementSystem
  │   └── 所有 FlowFieldAgent 沿流场移动
  │
  ├── TowerTargetingSystem
  │   ├── foreach Tower with TowerData+TowerState:
  │   │   ├── 遍历范围内 Enemy entities
  │   │   └── 选择最近的作为 target
  │   └── 写入 TowerState.CurrentTarget
  │
  ├── TowerAttackSystem
  │   ├── foreach Tower with valid target:
  │   │   ├── 计时器 += deltaTime
  │   │   ├── if timer >= interval:
  │   │   │   ├── ECB.CreateEntity (子弹)
  │   │   │   ├── 设置 BulletData + BulletState
  │   │   │   └── 重置计时器
  │   │   └── endif
  │   └── endforeach
  │
  ├── BulletMovementSystem
  │   ├── foreach Bullet:
  │   │   ├── 获取目标位置 (ComponentLookup<LocalTransform>)
  │   │   ├── 计算方向 → 移动
  │   │   ├── 累加飞行距离
  │   │   └── if 超出MaxRange → 标记销毁
  │   └── endforeach
  │
  ├── BulletHitSystem
  │   ├── foreach Bullet 未命中:
  │   │   ├── 检测与目标距离 < hitRadius
  │   │   ├── if 命中:
  │   │   │   ├── 扣减目标 HealthData.CurrentHP
  │   │   │   ├── 标记 HasHit = true
  │   │   │   └── 添加 DestroyTag
  │   │   └── endif
  │   └── endforeach
  │
  ├── HealthDeathSystem
  │   ├── foreach Entity with HealthData:
  │   │   └── if HP <= 0 → 添加 DeadTag
  │   └── endforeach
  │
  ├── EnemyGoalReachedSystem
  │   ├── foreach FlowFieldAgent with ReachedGoal:
  │   │   ├── 触发扣血逻辑(预留接口)
  │   │   └── 添加 DestroyTag
  │   └── endforeach
  │
  └── EntityCleanupSystem
      └── foreach Entity with DestroyTag:
          └── ECB.DestroyEntity / 回收到对象池
```

### 4.3 敌人生成流程
```
EnemySpawnSystem (InitializationSystemGroup):
  │
  ├── 读取 EnemySpawnTimer singleton
  ├── timer += deltaTime
  ├── if timer >= spawnInterval:
  │   ├── 选择随机出生点
  │   ├── ECB.Instantiate(enemyPrefabEntity)
  │   ├── 设置 LocalTransform 到出生点世界坐标
  │   ├── 添加 FlowFieldAgent + EnemyData + HealthData + EnemyTag
  │   └── 重置计时器
  └── endif
```

## 5. 性能优化方案

### 5.1 Job 并行策略
- **TowerTargetingSystem**: 使用 `NativeParallelMultiHashMap<int, Entity>` 空间哈希，将敌人按格子分桶，炮塔只查询周围格子内的敌人
- **BulletMovementSystem**: `IJobEntity` + `ScheduleParallel` 全并行
- **BulletHitSystem**: 使用 `ComponentLookup<HealthData>` 写入伤害，`NativeQueue<Entity>` 收集待销毁实体

### 5.2 空间哈希加速
- 敌人按所在格子坐标哈希到 `NativeParallelMultiHashMap<int, EnemyInfo>`
- 炮塔搜索时只遍历攻击范围覆盖的格子桶，O(范围格子数) 而非 O(全部敌人)
- 10万敌人场景下将 O(N×M) 降低为 O(N×K)，K为范围内格子数

### 5.3 对象池策略
- 子弹实体: 不直接 Destroy，而是 `Disable` + 移到回收区，下次发射时复用
- 使用 `EntityCommandBuffer` 批量处理创建/销毁
- 敌人实体: 死亡后回收到池，生成时优先从池取

### 5.4 LOD / 降频
- 超出相机范围的单位：降低更新频率至 1/4
- 远距离炮塔：降低搜敌频率

## 6. 风险点与应对

| 风险 | 影响 | 应对 |
|------|------|------|
| ComponentLookup 随机访问性能 | BulletHitSystem 写入HealthData | 使用 `[NativeDisableParallelForRestriction]` + 保证同一目标不被多子弹同帧命中（通过 HasHit 标记） |
| 大量 ECB 结构变更 | 帧尾 Playback 卡顿 | 使用 `BeginSimulation/EndSimulation` ECB 分摊 |
| 流场更新延迟 | 炮塔放置后敌人路径不更新 | 放置后触发 RebakeFlowField |
| 敌人越界 | 流场方向255(无方向)区域 | FlowFieldMoveJob 已有边界检查，补充格子中心吸附 |
| Input System 与 ECS 交互 | 主线程阻塞 | 输入逻辑在 MonoBehaviour 层处理，仅通过 ECB/事件传递到 ECS |

## 7. 文件结构

```
Assets/Scripts/TowerDefense/
├── Components/
│   ├── Combat/
│   │   ├── TowerComponents.cs      # TowerData, TowerState, TowerTag
│   │   ├── EnemyComponents.cs      # EnemyData, EnemyTag, HealthData
│   │   ├── BulletComponents.cs     # BulletData, BulletState, BulletTag
│   │   └── CombatTags.cs           # DeadTag, DestroyTag, DisabledTag
│   └── ... (existing)
├── Systems/
│   ├── Combat/
│   │   ├── TowerTargetingSystem.cs
│   │   ├── TowerAttackSystem.cs
│   │   ├── BulletMovementSystem.cs
│   │   ├── BulletHitSystem.cs
│   │   ├── HealthDeathSystem.cs
│   │   ├── EnemyGoalReachedSystem.cs
│   │   ├── EnemySpawnSystem.cs
│   │   └── EntityCleanupSystem.cs
│   └── ... (existing)
├── Bridge/
│   ├── CombatBridgeSystem.cs
│   └── TowerPlacementSystem.cs
├── Data/
│   ├── CombatConfig.cs             # 战斗配置数据
│   └── ... (existing)
├── Events/
│   └── GamePlayEvents/
│       └── CombatEvents.cs         # 战斗相关事件
└── Utilities/
    ├── SpatialHash.cs              # 空间哈希工具
    └── ... (existing)
```

