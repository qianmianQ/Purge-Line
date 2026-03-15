# EntityData 架构设计文档

## 1. 架构概述

EntityData 架构是一个为 Unity 塔防游戏设计的高性能、可扩展实体数据管理系统。它采用分层设计，结合 Addressables 资源系统、MemoryPack 序列化和类型安全的枚举系统，提供完整的运行时数据访问和编辑器工作流。

### 1.1 核心目标

- **类型安全**: 使用生成的强类型枚举（TurretId, EnemyId, ProjectileId）替代魔法数字
- **高性能**: 本地缓存 + 异步加载，避免运行时阻塞
- **热更新友好**: 支持运行时数据热更新和版本管理
- **可扩展**: 模块化设计，易于添加新的实体类型
- **编辑器友好**: 完整的可视化编辑工具链

### 1.2 架构分层

```
┌─────────────────────────────────────────────────────────────────┐
│                    Editor Layer (编辑器层)                      │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────────┐  │
│  │EntityDataHub │ │ SingleConfig │ │  Typed Editors       │  │
│  │   Window     │ │    Editor    │  (Turret/Enemy/etc)   │  │
│  └──────────────┘ └──────────────┘ └──────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                 Service Layer (服务层)                         │
│  ┌────────────────────────────────────────────────────────┐  │
│  │              IEntityDataService                         │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │  │
│  │  │   GetXXX    │  │   Cache     │  │  Hot Update     │  │  │
│  │  │   Async     │  │   Manager   │  │  Notify         │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                  Data Layer (数据层)                           │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐  │
│  │   Entity   │ │   Turret   │ │    Enemy   │ │ Projectile │  │
│  │   Address  │ │   Config   │ │   Config   │ │   Config   │  │
│  │    Index   │ │   Package  │ │   Package  │ │   Package  │  │
│  └────────────┘ └────────────┘ └────────────┘ └────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│              Storage Layer (存储层)                            │
│  ┌─────────────────┐  ┌───────────────────────────────────────┐  │
│  │ Addressables    │  │        MemoryPack (.bytes files)      │  │
│  │  (Index +       │  │   - Index: entity_address_index      │  │
│  │   Config Assets)│  │   - Configs: turret_001, enemy_001...  │  │
│  └─────────────────┘  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## 2. 核心组件详解

### 2.1 类型系统

#### EntityType 枚举
```csharp
public enum EntityType
{
    TURRET = 0,
    ENEMY = 1,
    PROJECTILE = 2,
    Max = 3
}
```

#### 生成的强类型 ID 枚举（示例）
```csharp
public enum TurretId
{
    None = 0,
    Turret_Basic = 1,
    Turret_Sniper = 2,
    // ... 自动生成
}
```

### 2.2 数据契约

#### IEntityConfigPackage 接口
所有实体配置包必须实现此接口：

```csharp
public interface IEntityConfigPackage
{
    EntityType EntityType { get; }
    string EntityIdToken { get; set; }
    string EntityBlueprintGuid { get; set; }
    string ExtraSfxAddress { get; set; }
    int Version { get; set; }
    bool IsDirty { get; set; }
    int SchemaVersion { get; set; }
    string DisplayNameForLog { get; }
    void Normalize();
}
```

#### 配置包结构（以 TurretConfigPackage 为例）

```csharp
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class TurretConfigPackage : IEntityConfigPackage
{
    public const int CurrentSchemaVersion = 2;

    // 核心标识
    [MemoryPackOrder(1)] public string EntityIdToken { get; set; } = string.Empty;

    // 业务数据
    [MemoryPackOrder(2)] public TurretBaseData Base { get; set; } = new TurretBaseData();
    [MemoryPackOrder(50)] public TurretUIData Ui { get; set; } = new TurretUIData();

    // 资源引用
    [MemoryPackOrder(100)] public string EntityBlueprintGuid { get; set; } = string.Empty;
    [MemoryPackOrder(150)] public string ExtraSfxAddress { get; set; } = string.Empty;

    // 版本控制
    [MemoryPackOrder(200)] public int Version { get; set; } = 1;
    [MemoryPackOrder(201)] public bool IsDirty { get; set; }
    [MemoryPackOrder(202)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    // 计算属性
    [MemoryPackIgnore] public EntityType EntityType => EntityType.TURRET;
    [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;
}
```

### 2.3 地址索引系统

#### EntityAddressIndex
用于运行时快速查找实体配置的地址映射：

```csharp
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class EntityAddressIndex
{
    [MemoryPackOrder(1)] public int SchemaVersion { get; set; } = 1;
    [MemoryPackOrder(50)] public List<EntityTypeAddressBucket> TypeBuckets { get; set; } = new();

    public bool TryGetAddress(EntityType entityType, int localId, out string address);
    public bool TryGetKeyByAddress(string address, out EntityType entityType, out int localId, out string entityToken);
    public bool TryGetEntityToken(EntityType entityType, int localId, out string token);
}
```

#### 地址规则
```csharp
public static class EntityDataAddressRules
{
    public const string IndexAddress = "entity_address_index";

    public static string BuildEntityConfigAddress(EntityType entityType, string entityIdToken)
    {
        string prefix = entityType switch
        {
            EntityType.TURRET => "turret",
            EntityType.ENEMY => "enemy",
            EntityType.PROJECTILE => "projectile",
            _ => "entity"
        };
        return $"{prefix}_{entityIdToken}".ToLowerInvariant();
    }
}
```

### 2.4 实体数据服务

#### EntityDataService 核心功能

```csharp
public sealed class EntityDataService : IEntityDataService
{
    // 初始化
    public async UniTask InitializeAsync();

    // 类型安全的数据获取
    public async UniTask<TurretConfigPackage> GetTurretAsync(TurretId turretId);
    public async UniTask<EnemyConfigPackage> GetEnemyAsync(EnemyId enemyId);
    public async UniTask<ProjectileConfigPackage> GetProjectileAsync(ProjectileId projectileId);

    // 缓存管理
    public bool TryGetCached(EntityType entityType, int localId, out IEntityConfigPackage package);

    // 运行时实例映射
    public void RegisterRuntimeInstance(object instance, EntityType entityType, int localId);
    public void UnregisterRuntimeInstance(object instance);
    public bool TryGetEntityDataByInstance(object instance, out EntityType entityType, out int localId, out IEntityConfigPackage package);

    // 热更新支持
    public async UniTask<bool> NotifyHotUpdateByAddressAsync(string address);
    public bool ApplyRuntimeMutation(EntityType entityType, int localId, Action<IEntityConfigPackage> mutator, string reason);

    // 变更通知
    public event Action<EntityDataChangeEvent> EntityDataChanged;
}
```

### 2.5 编辑器工具链

#### EntityDataHubWindow - 中央管理窗口
- 索引重建
- 配置验证
- 实体列表浏览
- 快速打开单配置编辑器

#### SingleEntityConfigEditorAsset + Editor
- ScriptableObject 包装器用于编辑单个实体配置
- 支持 Turret/Enemy/Projectile 等类型特定编辑器

#### EntityIdEnumGenerator
- 根据配置自动生成强类型 ID 枚举
- 确保编辑器-运行时类型一致性

## 3. 数据流

### 3.1 运行时加载流程

```
1. 游戏启动
   ↓
2. EntityDataService.InitializeAsync()
   ├── 加载索引文件 (entity_address_index.bytes)
   ├── 反序列化 EntityAddressIndex
   └── 构建运行时映射表 (_keyToAddress, _addressToKey)
   ↓
3. 业务系统请求数据
   GetTurretAsync(TurretId.BasicTurret)
   ↓
4. 检查缓存
   ├── 命中 → 直接返回缓存的 TurretConfigPackage
   └── 未命中 → 继续加载
       ↓
5. 地址查找
   _keyToAddress[(EntityType.TURRET, 1)] → "turret_basic_turret"
   ↓
6. 异步加载 (Addressables)
   LoadAssetAsync<TextAsset>("turret_basic_turret")
   ↓
7. 反序列化
   MemoryPackSerializer.Deserialize<TurretConfigPackage>(bytes)
   ↓
8. 缓存并返回
   _cache[key] = package
   return package
```

### 3.2 热更新流程

```
1. 外部系统检测到资源更新
   ↓
2. 调用 NotifyHotUpdateByAddressAsync(address)
   ↓
3. 地址 → EntityKey 映射查找
   _addressToKey[address] → EntityKey
   ↓
4. 重新加载配置
   LoadPackageByAddressAsync(key, address)
   ↓
5. 更新缓存
   _cache[key] = newPackage
   newPackage.Version += 1
   newPackage.IsDirty = true
   ↓
6. 触发变更事件
   EntityDataChanged?.Invoke(new EntityDataChangeEvent(...))
```

### 3.3 编辑器工作流

```
1. 策划/开发者在 EntityDataHubWindow 中
   ├── 创建新实体 → 生成 SingleEntityConfigEditorAsset
   └── 或编辑现有实体
   ↓
2. SingleEntityConfigEditorAssetEditor 打开
   └── 显示类型特定的编辑器界面
       ├── Turret: 攻击范围、攻击速度、成本等
       ├── Enemy: 生命值、移动速度、奖励等
       └── Projectile: 速度、伤害、生命周期等
   ↓
3. 保存配置
   ├── 序列化为 .bytes 文件 (MemoryPack)
   └── 存储到 Addressables 可加载路径
   ↓
4. 重建索引 (可选/自动)
   ├── EntityIdEnumGenerator 重新生成枚举
   └── EntityAddressIndex 重新构建
```

## 4. 关键设计决策

### 4.1 为什么使用 MemoryPack 而不是 JSON？

| 特性 | MemoryPack | JSON |
|------|-----------|------|
| 序列化速度 | 极快 (二进制) | 较慢 (文本解析) |
| 包大小 | 紧凑 | 较大 |
| 版本兼容性 | 内置 VersionTolerant | 需手动处理 |
| Unity IL2CPP 兼容性 | 优秀 | 中等 |
| 人类可读性 | 否 | 是 |

**决策**: 游戏配置数据不需要人类直接编辑（通过编辑器工具），优先考虑性能和包大小。

### 4.2 为什么分离索引和配置数据？

```
索引文件 (entity_address_index.bytes): ~几KB
├── 包含所有实体的类型、ID、地址映射
└── 运行时始终驻留内存

配置文件 (turret_xxx.bytes): 每个 ~1-5KB
├── 仅在需要时加载
└── 可卸载释放内存
```

**优势**:
1. 快速启动 - 只需加载小索引
2. 按需加载 - 减少初始内存占用
3. 灵活卸载 - 内存紧张时可释放不常用配置

### 4.3 为什么使用强类型 ID 枚举？

**问题**: `int` 类型 ID 容易混淆和误用
```csharp
// 容易出错的写法
LoadConfig(1, "turret");  // 1 是什么？
LoadConfig(1, "enemy");   // 和上面相同，但意义完全不同
```

**解决方案**: 强类型枚举
```csharp
// 类型安全的写法
LoadConfig(TurretId.BasicTurret);      // 明确是炮塔
LoadConfig(EnemyId.BasicEnemy);        // 明确是敌人

// 编译器会阻止这种错误
TurretId id = EnemyId.Goblin;  // 编译错误！
```

## 5. 接口契约

### 5.1 IEntityDataService 完整契约

```csharp
public interface IEntityDataService
{
    // ========== 生命周期 ==========
    /// <summary>
    /// 初始化服务，加载索引并构建映射表
    /// 线程安全，可多次调用（幂等）
    /// </summary>
    UniTask InitializeAsync();

    // ========== 数据获取（类型安全）==========
    /// <summary>
    /// 获取炮塔配置，如果未缓存则异步加载
    /// 不会返回 null，失败时返回 Fallback 配置
    /// </summary>
    UniTask<TurretConfigPackage> GetTurretAsync(TurretId turretId);

    UniTask<EnemyConfigPackage> GetEnemyAsync(EnemyId enemyId);
    UniTask<ProjectileConfigPackage> GetProjectileAsync(ProjectileId projectileId);

    // ========== 缓存管理 ==========
    /// <summary>
    /// 尝试从缓存获取配置（同步）
    /// </summary>
    bool TryGetCached(EntityType entityType, int localId, out IEntityConfigPackage package);

    // ========== 运行时实例映射 ==========
    /// <summary>
    /// 注册运行时实例与实体数据的映射关系
    /// 用于从游戏对象反查配置数据
    /// </summary>
    void RegisterRuntimeInstance(object instance, EntityType entityType, int localId);
    void UnregisterRuntimeInstance(object instance);

    /// <summary>
    /// 通过运行时实例获取关联的配置数据
    /// </summary>
    bool TryGetEntityDataByInstance(object instance, out EntityType entityType,
        out int localId, out IEntityConfigPackage package);

    // ========== 热更新支持 ==========
    /// <summary>
    /// 通知指定地址的配置已热更新，重新加载并更新缓存
    /// 成功时触发 EntityDataChanged 事件
    /// </summary>
    UniTask<bool> NotifyHotUpdateByAddressAsync(string address);

    /// <summary>
    /// 对指定实体应用运行时变更
    /// 用于游戏内动态修改配置（如技能临时改变属性）
    /// </summary>
    bool ApplyRuntimeMutation(EntityType entityType, int localId,
        Action<IEntityConfigPackage> mutator, string reason);

    // ========== 事件通知 ==========
    /// <summary>
    /// 实体数据变更事件（热更新、运行时变更等）
    /// 包含变更原因、版本号等信息
    /// </summary>
    event Action<EntityDataChangeEvent> EntityDataChanged;
}
```

### 5.2 IEntityConfigPackage 契约

```csharp
public interface IEntityConfigPackage
{
    // ========== 核心标识 ==========
    EntityType EntityType { get; }           // 实体类型（TURRET/ENEMY/PROJECTILE）
    string EntityIdToken { get; set; }       // 唯一标识符（如 "basic_turret"）

    // ========== 资源引用 ==========
    string EntityBlueprintGuid { get; set; } // ECS 使用的蓝图 GUID
    string ExtraSfxAddress { get; set; }   // 额外音效资源地址

    // ========== 版本控制 ==========
    int Version { get; set; }                // 数据版本号（热更新时递增）
    bool IsDirty { get; set; }               // 是否被修改过
    int SchemaVersion { get; set; }            // 数据结构版本（兼容性）

    // ========== 辅助属性 ==========
    string DisplayNameForLog { get; }         // 用于日志显示的友好名称

    // ========== 生命周期方法 ==========
    void Normalize();                        // 规范化数据（填充默认值）
}
```

## 6. 数据存储格式

### 6.1 索引文件 (entity_address_index.bytes)

```csharp
[MemoryPackable]
public partial class EntityAddressIndex
{
    public int SchemaVersion { get; set; } = 1;
    public List<EntityTypeAddressBucket> TypeBuckets { get; set; } = new();
}

public partial class EntityTypeAddressBucket
{
    public EntityType EntityType { get; set; }
    public List<EntityAddressItem> Items { get; set; } = new();
}

public partial class EntityAddressItem
{
    public int LocalId { get; set; }           // 枚举值 (1, 2, 3...)
    public string EntityIdToken { get; set; }   // "basic_turret"
    public string EnumName { get; set; }        // "Turret_Basic"
    public string Address { get; set; }         // "turret_basic_turret"
}
```

### 6.2 配置包文件 (turret_xxx.bytes)

以 MemoryPack 二进制格式存储，具体结构见各 XXXConfigPackage 类定义。

## 7. 线程安全与性能考虑

### 7.1 线程安全

- **InitializeAsync**: 幂等，内部有状态检查
- **GetXXXAsync**: 安全，依赖 ConcurrentDictionary（或普通 Dictionary + 调用约束）
- **缓存更新**: 单线程上下文执行（Unity 主线程）

### 7.2 性能优化

- **索引常驻**: 小体积索引始终内存驻留
- **配置按需**: 仅在首次访问时加载配置
- **地址复用**: Addressables 自动处理重复加载
- **Zero Allocation**: 大量使用 struct 和泛型约束

## 8. 错误处理与降级策略

### 8.1 Fallback 机制

任何加载失败都会返回 Fallback 配置而非 null：

```csharp
public static TurretConfigPackage BuildFallback(string token, string reason)
{
    return new TurretConfigPackage
    {
        EntityIdToken = token,
        Base = new TurretBaseData { Name = token, Description = reason },
        Ui = new TurretUIData { DisplayName = token, Description = reason, ThemeColorHex = "#FF0000FF" },
        IsDirty = true
    };
}
```

### 8.2 错误分类

| 错误类型 | 处理方式 | 返回值 |
|---------|---------|-------|
| 索引加载失败 | 记录错误，返回空索引 | 空 EntityAddressIndex |
| 配置地址不存在 | 记录错误，返回 Fallback | Fallback Package |
| 配置反序列化失败 | 记录错误，返回 Fallback | Fallback Package |
| 网络/IO 错误 | 记录错误，返回 Fallback | Fallback Package |

---

**文档版本**: 1.0
**最后更新**: 2026-03-15
**维护者**: Claude Code
