# EntityData 架构评估报告

**评估日期**: 2026-03-15
**评估范围**: EntityData 完整架构（运行时 + 编辑器）
**评估维度**: 运行时性能、内存使用、可扩展性、开发效率、维护成本

---

## 1. 执行摘要

### 1.1 总体评分

| 维度 | 评分 | 权重 | 加权得分 |
|-----|------|-----|---------|
| 运行时性能 | 8.5/10 | 25% | 2.13 |
| 内存效率 | 7.5/10 | 20% | 1.50 |
| 可扩展性 | 8.0/10 | 20% | 1.60 |
| 开发效率 | 9.0/10 | 20% | 1.80 |
| 维护成本 | 7.0/10 | 15% | 1.05 |
| **总分** | | | **8.08/10** |

### 1.2 关键结论

**优势**:
- 完整的类型安全设计，编译期捕获大部分错误
- 高性能的二进制序列化（MemoryPack）
- 完善的编辑器工具链，提升开发效率
- 热更新支持，适合 LiveOps 需求

**风险**:
- 添加新实体类型需要修改多处代码（枚举、Package 类、服务方法、编辑器）
- 运行时缓存无上限，长时间运行可能内存膨胀
- 索引查找使用线性搜索，实体数量大时性能下降

---

## 2. 运行时性能评估

### 2.1 序列化性能

**MemoryPack vs 其他序列化器**（基准测试数据，仅供参考）

| 序列化器 | 序列化速度 | 反序列化速度 | 包大小 | 版本兼容 |
|---------|----------|------------|-------|---------|
| MemoryPack | ~1.5x JSON | ~2.0x JSON | ~60% JSON | 原生支持 |
| JSON (System.Text) | 基准 | 基准 | 基准 | 需手动处理 |
| MessagePack | ~1.2x JSON | ~1.5x JSON | ~50% JSON | 原生支持 |
| Protobuf | ~0.8x JSON | ~1.0x JSON | ~40% JSON | 原生支持 |

**评估**: MemoryPack 在 Unity 环境下表现出色，特别是对于 C# 强类型对象的序列化。

### 2.2 运行时操作性能分析

#### 2.2.1 索引查找性能

**当前实现**: 线性搜索 O(n)

```csharp
// EntityAddressIndex.cs
public bool TryGetAddress(EntityType entityType, int localId, out string address)
{
    for (int i = 0; i < TypeBuckets.Count; i++)  // 遍历所有类型桶
    {
        var bucket = TypeBuckets[i];
        if (bucket.EntityType != entityType)
            continue;

        for (int j = 0; j < bucket.Items.Count; j++)  // 遍历该类型所有实体
        {
            var item = bucket.Items[j];
            if (item.LocalId == localId)
            {
                address = item.Address;
                return true;
            }
        }
    }
    // ...
}
```

**性能估算**:

| 实体总数 | 平均查找时间 | 评估 |
|---------|------------|-----|
| 50 | ~0.5 μs | 优秀 |
| 200 | ~2 μs | 良好 |
| 500 | ~5 μs | 可接受 |
| 2000 | ~20 μs | 需优化 |
| 10000 | ~100 μs |  unacceptable |

**优化建议**:

```csharp
// 建议：使用 Dictionary 优化查找
public partial class EntityAddressIndex
{
    // 运行时构建的查找表（不序列化）
    [MemoryPackIgnore]
    private Dictionary<(EntityType, int), string> _addressCache;

    public void BuildLookupCache()
    {
        _addressCache = new Dictionary<(EntityType, int), string>();
        foreach (var bucket in TypeBuckets)
        {
            foreach (var item in bucket.Items)
            {
                _addressCache[(bucket.EntityType, item.LocalId)] = item.Address;
            }
        }
    }

    public bool TryGetAddressFast(EntityType entityType, int localId, out string address)
    {
        return _addressCache.TryGetValue((entityType, localId), out address);
    }
}
```

#### 2.2.2 缓存管理性能

**当前实现**:

```csharp
private readonly Dictionary<EntityKey, IEntityConfigPackage> _cache = new();
```

**特点**:
- 无容量限制
- 无过期策略
- 无优先级管理

**风险评估**:

| 场景 | 内存占用 | 风险等级 |
|-----|---------|---------|
| 小型游戏（50实体） | ~500KB | 低 |
| 中型游戏（200实体） | ~2MB | 中 |
| 大型游戏（1000+实体） | ~10MB+ | 高 |
| 长时间运行（热更新频繁） | 持续增长 | 高 |

**优化建议**:

```csharp
// 建议：添加 LRU 缓存策略
public sealed class EntityDataService : IEntityDataService
{
    private readonly LRUCache<EntityKey, IEntityConfigPackage> _cache;

    public EntityDataService(int cacheCapacity = 100)
    {
        _cache = new LRUCache<EntityKey, IEntityConfigPackage>(cacheCapacity);
    }
}
```

### 2.3 异步操作性能

**加载延迟估算**:

| 来源 | 平均延迟 | 适用场景 |
|-----|---------|---------|
| 本地缓存 | ~0.1 ms | 已加载过的配置 |
| 本地磁盘 (SSD) | ~5-10 ms | Addressables 本地缓存 |
| 远程 CDN | ~50-200 ms | 首次下载/热更新 |

**优化策略**:

```csharp
// 预加载常用配置
public async UniTask PreloadCommonConfigs()
{
    var commonTurrets = new[] { TurretId.Basic, TurretId.Sniper, TurretId.Cannon };
    var tasks = commonTurrets.Select(id => GetTurretAsync(id).AsUniTask()).ToArray();
    await UniTask.WhenAll(tasks);
}
```

---

## 3. 内存效率评估

### 3.1 内存占用分析

#### 3.1.1 索引内存占用

```csharp
// EntityAddressIndex 内存估算
public partial class EntityAddressIndex
{
    // 假设：1000 个实体，平均 token 长度 20 字符
    int SchemaVersion;        // 4 bytes
    List<EntityTypeAddressBucket> TypeBuckets;  // 引用 + 列表开销
}

// 估算：1000 实体 ≈ 100-200KB
```

#### 3.1.2 配置包内存占用

| 配置类型 | 典型大小 | 主要内存消耗 |
|---------|---------|------------|
| TurretConfigPackage | ~2-5KB | BaseData + UIData + 字符串 |
| EnemyConfigPackage | ~1-3KB | BaseData + UIData |
| ProjectileConfigPackage | ~0.5-1KB | BaseData + UIData |

**总内存估算**:

| 场景 | 缓存实体数 | 内存占用 | 评估 |
|-----|-----------|---------|-----|
| 最小 (50) | 50 | ~200KB | 优秀 |
| 典型 (200) | 200 | ~800KB | 优秀 |
| 大型 (1000) | 1000 | ~4MB | 良好 |
| 全部缓存 | 2000 | ~8MB | 可接受 (需监控) |

### 3.2 内存分配分析

#### 3.2.1 GC 压力评估

| 操作 | 堆分配 | 频率 | 优化建议 |
|-----|-------|-----|---------|
| 反序列化 | 高（新对象） | 按需 | 使用对象池 |
| 索引查找 | 低（struct key） | 高频 | 使用字典缓存 |
| 字符串操作 | 中 | 中 | 减少字符串拼接 |
| 事件回调 | 中（闭包分配） | 低 | 使用结构体事件 |

**关键优化点**:

```csharp
// 问题：每次查找都创建新的 EntityKey struct（虽然开销小，但...）
var key = new EntityKey(entityType, localId);  // 每次都是新分配

// 优化：如果 EntityKey 是 readonly struct，编译器会优化为栈分配
// 当前实现已经是 readonly struct，无需额外优化

// 更大的问题：字典的 GetHashCode 和 Equals
// 当前实现已经正确重载，性能良好
```

#### 3.2.2 对象池机会

```csharp
// 建议：配置包对象池（适合频繁创建/销毁的场景）
public class EntityConfigPackagePool<T> where T : class, IEntityConfigPackage, new()
{
    private readonly ObjectPool<T> _pool;

    public EntityConfigPackagePool()
    {
        _pool = new ObjectPool<T>(
            createFunc: () => new T(),
            actionOnGet: pkg => pkg.Reset(),
            actionOnRelease: pkg => { },
            actionOnDestroy: pkg => { },
            defaultCapacity: 10,
            maxSize: 50
        );
    }
}
```

### 3.3 内存泄漏风险

| 风险点 | 风险等级 | 说明 | 缓解措施 |
|-------|---------|-----|---------|
| 缓存无限增长 | 中 | 无容量限制 | 添加 LRU 策略 |
| 运行时实例映射 | 低 | Unregister 可能遗漏 | 使用弱引用或定期清理 |
| 事件订阅 | 低 | EntityDataChanged 可能累积订阅 | 使用弱事件模式 |
| Addressables 句柄 | 中 | 加载后未显式释放 | 当前使用 TextAsset 方式，自动管理 |

---

## 4. 可扩展性评估

### 4.1 添加新实体类型的复杂度分析

假设要添加新类型 `TRAP`（陷阱），需要修改的文件：

#### 4.1.1 数据层修改

| 文件 | 修改内容 | 复杂度 |
|-----|---------|-------|
| `EntityType.cs` | 添加 `TRAP = 3` | 低 |
| `EntityDataContracts.cs` | 创建 `TrapConfigPackage` 类 | 中 |
| `EntityTypedPackages.cs` | 添加 `TrapBaseData`, `TrapUIData` | 中 |

#### 4.1.2 服务层修改

| 文件 | 修改内容 | 复杂度 |
|-----|---------|-------|
| `IEntityDataService.cs` | 添加 `GetTrapAsync(TrapId id)` | 低 |
| `EntityDataService.cs` | 实现 `GetTrapAsync` 和 `BuildFallback` case | 中 |

#### 4.1.3 编辑器层修改

| 文件 | 修改内容 | 复杂度 |
|-----|---------|-------|
| `EntityIdEnumGenerator.cs` | 添加 `TrapId` 枚举生成逻辑 | 中 |
| `EntityConfigRegistryAsset.cs` | 添加 `Trap` 类型支持 | 低 |
| `EntityTypedSingleEditors.cs` | 创建 `TrapConfigEditor` 类 | 高 |
| `EntityDataHubWindow.cs` | 添加 `Trap` 类型过滤和显示 | 低 |

#### 4.1.4 总复杂度评估

```
添加新实体类型的总工作量估计：

├── 必须修改文件: ~12 个
├── 新增代码行数估计: ~800-1200 行
├── 开发时间估计:
│   ├── 熟练开发者: 4-6 小时
│   ├── 普通开发者: 1-2 天
│   └── 含测试和文档: 2-3 天
└── 风险等级: 中等
    ├── 容易遗漏的修改点: 编辑器工具链
    └── 潜在问题: 类型转换、枚举值冲突
```

### 4.2 扩展现有实体类型的字段

相比添加新类型，添加字段要简单得多：

```csharp
// 1. 在 XxxBaseData 或 XxxUIData 中添加字段
[MemoryPackable]
public partial class TurretBaseData
{
    // 现有字段...
    [MemoryPackOrder(10)] public float NewStat { get; set; }  // 新字段
}

// 2. 更新 SchemaVersion（如果需要兼容性处理）
public const int CurrentSchemaVersion = 3;  // 从 2 升级到 3

// 3. 更新编辑器工具（如有需要）
// 在 TurretConfigEditor 中添加对应字段的 UI
```

**MemoryPack 版本兼容性**:
- 新增字段默认使用默认值
- 删除字段会被忽略
- 重命名字段需要自定义格式化器

### 4.3 扩展示例：添加新实体类型的完整步骤

假设我们要添加 `OBSTACLE`（障碍物）类型：

#### 步骤 1: 修改 EntityType
```csharp
public enum EntityType
{
    TURRET = 0,
    ENEMY = 1,
    PROJECTILE = 2,
    OBSTACLE = 3,  // 新增
    Max = 4
}
```

#### 步骤 2: 创建数据契约
```csharp
// ObstacleConfigPackage.cs
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class ObstacleConfigPackage : IEntityConfigPackage
{
    public const int CurrentSchemaVersion = 1;

    [MemoryPackOrder(1)] public string EntityIdToken { get; set; } = string.Empty;
    [MemoryPackOrder(2)] public ObstacleBaseData Base { get; set; } = new();
    [MemoryPackOrder(50)] public ObstacleUIData Ui { get; set; } = new();
    [MemoryPackOrder(100)] public string EntityBlueprintGuid { get; set; } = string.Empty;
    [MemoryPackOrder(200)] public int Version { get; set; } = 1;
    [MemoryPackOrder(201)] public bool IsDirty { get; set; }
    [MemoryPackOrder(202)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [MemoryPackIgnore] public EntityType EntityType => EntityType.OBSTACLE;
    [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;
    [MemoryPackIgnore] public string ExtraSfxAddress { get; set; } = string.Empty;

    public void Normalize()
    {
        Base ??= new ObstacleBaseData();
        Ui ??= new ObstacleUIData();
        EntityIdToken ??= string.Empty;
        EntityBlueprintGuid ??= string.Empty;
        // ... 其他默认值
    }

    public static ObstacleConfigPackage BuildFallback(string token, string reason)
    {
        return new ObstacleConfigPackage
        {
            EntityIdToken = token,
            Base = new ObstacleBaseData { Name = token, Description = reason },
            Ui = new ObstacleUIData { DisplayName = token, Description = reason, ThemeColorHex = "#FF0000FF" },
            IsDirty = true
        };
    }
}

[MemoryPackable]
public partial class ObstacleBaseData
{
    [MemoryPackOrder(1)] public string Name { get; set; } = string.Empty;
    [MemoryPackOrder(2)] public string Description { get; set; } = string.Empty;
    [MemoryPackOrder(3)] public float MaxHp { get; set; } = 100f;
    [MemoryPackOrder(4)] public bool IsDestructible { get; set; } = false;
    [MemoryPackOrder(5)] public float BlockPathfindingRadius { get; set; } = 1f;
}

[MemoryPackable]
public partial class ObstacleUIData
{
    [MemoryPackOrder(1)] public string DisplayName { get; set; } = string.Empty;
    [MemoryPackOrder(2)] public string Description { get; set; } = string.Empty;
    [MemoryPackOrder(3)] public string IconAddress { get; set; } = string.Empty;
    [MemoryPackOrder(4)] public string PreviewAddress { get; set; } = string.Empty;
    [MemoryPackOrder(5)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
}
```

#### 步骤 3: 扩展服务接口
```csharp
// IEntityDataService.cs
public interface IEntityDataService
{
    // ... 现有方法 ...

    // 新增
    UniTask<ObstacleConfigPackage> GetObstacleAsync(ObstacleId obstacleId);
}

// EntityDataService.cs
public sealed class EntityDataService : IEntityDataService
{
    // ... 现有代码 ...

    public async UniTask<ObstacleConfigPackage> GetObstacleAsync(ObstacleId obstacleId)
    {
        return await GetTypedAsync(EntityType.OBSTACLE, (int)obstacleId,
            (token, reason) => ObstacleConfigPackage.BuildFallback(token, reason),
            package => package as ObstacleConfigPackage);
    }

    private static IEntityConfigPackage BuildFallback(EntityType entityType, string token, string reason)
    {
        switch (entityType)
        {
            // ... 现有 case ...
            case EntityType.OBSTACLE:
                return ObstacleConfigPackage.BuildFallback(token, reason);
            default:
                // ...
        }
    }
}
```

#### 步骤 4: 更新编辑器工具
```csharp
// EntityIdEnumGenerator.cs
private void GenerateAllEnums()
{
    // ... 现有代码 ...
    GenerateEnum<EntityType.OBSTACLE>("ObstacleId");
}

// EntityTypedSingleEditors.cs
public class ObstacleConfigEditor : SingleEntityConfigEditor<ObstacleConfigPackage, ObstacleBaseData, ObstacleUIData>
{
    protected override EntityType EntityType => EntityType.OBSTACLE;

    protected override void DrawBaseDataFields(ObstacleBaseData baseData)
    {
        baseData.Name = EditorGUILayout.TextField("Name", baseData.Name);
        baseData.MaxHp = EditorGUILayout.FloatField("Max HP", baseData.MaxHp);
        baseData.IsDestructible = EditorGUILayout.Toggle("Is Destructible", baseData.IsDestructible);
        // ...
    }
}
```

### 4.4 扩展性改进建议

#### 方案 A: 反射/代码生成自动化

**目标**: 减少添加新类型时的重复代码

```csharp
// 使用 Source Generator 自动生成服务方法
[GenerateEntityDataServiceMethod]
public partial class EntityDataService
{
    // 自动生成：
    // - GetObstacleAsync
    // - BuildFallback case
}

// 使用反射注册编辑器
public class AutoEntityConfigEditor : SingleEntityConfigEditor
{
    public override void OnInspectorGUI()
    {
        var package = target as IEntityConfigPackage;

        // 反射遍历所有属性并绘制对应 UI
        foreach (var property in package.GetType().GetProperties())
        {
            DrawProperty(property);
        }
    }
}
```

**优点**:
- 添加新类型时代码量大幅减少
- 维护一致性

**缺点**:
- 反射性能开销
- 代码可读性降低
- 调试困难

#### 方案 B: 泛型化重构

**目标**: 使用泛型减少重复代码

```csharp
// 当前：每个类型一个方法
UniTask<TurretConfigPackage> GetTurretAsync(TurretId id);
UniTask<EnemyConfigPackage> GetEnemyAsync(EnemyId id);
// ... 更多

// 提议：泛型方法
UniTask<TPackage> GetAsync<TPackage, TId>(TId id)
    where TPackage : class, IEntityConfigPackage
    where TId : struct, Enum;

// 使用
var turret = await GetAsync<TurretConfigPackage, TurretId>(TurretId.Basic);
var enemy = await GetAsync<EnemyConfigPackage, EnemyId>(EnemyId.Goblin);
```

**优点**:
- 大幅减少服务层代码
- 类型安全保持
- 编译期优化更好

**缺点**:
- 调用语法稍复杂
- 需要添加类型映射配置

**推荐**: 方案 B 是更平衡的选择，既保持性能又减少维护负担。

---

## 5. 详细评估维度分析

### 5.1 运行时性能详细分析

#### 5.1.1 启动性能

**启动阶段耗时分解**:

```
1. 加载索引文件 (entity_address_index.bytes)
   ├── Addressables 加载: ~10-50ms
   ├── 反序列化: ~1-5ms (1000实体)
   └── 构建映射表: ~1-2ms

2. 服务初始化
   └── 对象创建: ~0.1ms

总计: ~15-60ms (可接受)
```

**优化机会**:
- 索引文件可进一步压缩
- 使用异步初始化避免阻塞主线程

#### 5.1.2 运行时查询性能

**缓存命中场景**:

```csharp
// 第一次加载（缓存未命中）
var turret = await GetTurretAsync(TurretId.Basic);
// 耗时: ~20-100ms (取决于 Addressables 来源)

// 第二次加载（缓存命中）
var sameTurret = await GetTurretAsync(TurretId.Basic);
// 耗时: ~0.01ms (字典查找)
```

**性能比**: 缓存命中比未命中快 **1000-10000 倍**

### 5.2 内存效率详细分析

#### 5.2.1 对象内存布局

**TurretConfigPackage 内存布局**（64位系统）:

```
对象头: 24 bytes (Mono/IL2CPP)
引用字段 (8 bytes each):
  - EntityIdToken: 8
  - Base (TurretBaseData): 8
  - Ui (TurretUIData): 8
  - EntityBlueprintGuid: 8
  - ExtraSfxAddress: 8
值类型字段:
  - Version (int): 4
  - IsDirty (bool): 1 (padded to 4)
  - SchemaVersion (int): 4
  - EntityType (enum): 4
引用对象实际内存:
  - TurretBaseData 对象: ~100 bytes
  - TurretUIData 对象: ~150 bytes
  - 字符串 (平均 20 字符): ~60 bytes * 5 = 300 bytes

总计: ~24 + 56 + 16 + 100 + 150 + 300 = ~650 bytes/配置
```

#### 5.2.2 内存占用估算公式

```csharp
// 总内存 ≈ 索引内存 + 缓存内存
//
// 索引内存 ≈ 实体数量 * 平均字符串长度 * 2 + 集合开销
//          ≈ N * 50 * 2 + 10000
//          ≈ 100N + 10KB
//
// 缓存内存 ≈ 缓存实体数 * 平均配置大小
//          ≈ C * 650 bytes
//          ≈ 0.65 * C KB
//
// 示例：1000 实体，缓存 200 个
// 总内存 ≈ 110KB + 130KB = 240KB
```

### 5.3 可扩展性详细分析

#### 5.3.1 实体数量扩展

| 实体总数 | 启动时间 | 内存占用 | 查询性能 | 评估 |
|---------|---------|---------|---------|-----|
| 100 | ~20ms | ~50KB | ~0.5μs | 优秀 |
| 500 | ~35ms | ~200KB | ~2μs | 优秀 |
| 2000 | ~80ms | ~700KB | ~10μs | 良好 |
| 10000 | ~300ms | ~3MB | ~100μs | 需优化 |
| 50000 | ~2s | ~15MB | ~500μs | 不可接受 |

**扩展建议**:
- 超过 2000 实体时，索引查找应改用字典
- 超过 10000 实体时，考虑分片索引

#### 5.3.2 类型扩展

当前架构对新类型支持的工作量：

```
添加 1 个新类型 ≈
├── 数据层: 3 个文件 (~200 行)
├── 服务层: 2 个文件 (~100 行)
├── 编辑器层: 4 个文件 (~400 行)
└── 总计: ~700 行代码，~1-2 天工作量
```

**对比其他架构**:

| 架构类型 | 添加新类型工作量 | 灵活性 | 性能 |
|---------|---------------|-------|-----|
| 当前架构（类型安全） | 高（~700行） | 低 | 高 |
| 数据驱动（ScriptableObject） | 低（~50行） | 高 | 中 |
| 纯反射/JSON | 极低（~10行） | 极高 | 低 |

**建议**: 如果预期实体类型超过 10 种，应考虑数据驱动重构。

---

## 6. 风险与改进建议

### 6.1 高风险项

#### 风险 1: 缓存无上限导致 OOM

**风险等级**: 🔴 高

**描述**: 长时间运行游戏，如果频繁加载不同配置，缓存将持续增长。

**缓解措施**:
```csharp
// 1. 添加最大缓存容量限制
private const int MAX_CACHE_SIZE = 500;

// 2. 实现 LRU 淘汰策略
private readonly LRUCache<EntityKey, IEntityConfigPackage> _cache;

// 3. 提供显式清理接口
public void ClearCache(EntityType? entityType = null)
{
    if (entityType.HasValue)
    {
        // 清理特定类型
        var keysToRemove = _cache.Keys.Where(k => k.EntityType == entityType.Value).ToList();
        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }
    else
    {
        _cache.Clear();
    }
}
```

#### 风险 2: 线性索引查找性能退化

**风险等级**: 🟡 中

**描述**: 当前索引查找使用嵌套循环，时间复杂度 O(n)。实体数量超过 2000 时性能显著下降。

**缓解措施**:
```csharp
// 在运行时构建字典缓存
public partial class EntityAddressIndex
{
    [MemoryPackIgnore]
    private Dictionary<(EntityType, int), EntityAddressItem> _lookupCache;

    [MemoryPackIgnore]
    private Dictionary<string, (EntityType, int, string)> _addressCache;

    public void BuildLookupCache()
    {
        _lookupCache = new Dictionary<(EntityType, int), EntityAddressItem>();
        _addressCache = new Dictionary<string, (EntityType, int, string)>();

        foreach (var bucket in TypeBuckets)
        {
            foreach (var item in bucket.Items)
            {
                _lookupCache[(bucket.EntityType, item.LocalId)] = item;
                _addressCache[item.Address] = (bucket.EntityType, item.LocalId, item.EntityIdToken);
            }
        }
    }

    // O(1) 查找
    public bool TryGetAddressFast(EntityType entityType, int localId, out string address)
    {
        if (_lookupCache.TryGetValue((entityType, localId), out var item))
        {
            address = item.Address;
            return true;
        }
        address = null;
        return false;
    }
}
```

### 6.2 中风险项

#### 风险 3: 类型扩展重复代码

**风险等级**: 🟡 中

**描述**: 添加新实体类型需要复制大量相似代码，容易出错且维护困难。

**长期解决方案**:

**方案 A: 泛型重构（推荐）**

```csharp
// 统一的泛型接口
public interface IEntityDataService
{
    UniTask<TPackage> GetAsync<TPackage, TId>(TId id)
        where TPackage : class, IEntityConfigPackage
        where TId : struct, Enum;

    // 使用示例
    // var turret = await GetAsync<TurretConfigPackage, TurretId>(TurretId.Basic);
}

// 类型映射配置
public static class EntityTypeMapping
{
    public static readonly Dictionary<Type, EntityType> TypeToEntityType = new()
    {
        [typeof(TurretConfigPackage)] = EntityType.TURRET,
        [typeof(EnemyConfigPackage)] = EntityType.ENEMY,
        [typeof(ObstacleConfigPackage)] = EntityType.OBSTACLE,
    };
}
```

**方案 B: Source Generator 自动生成**

```csharp
// 使用属性标记
[EntityConfig(typeof(TurretBaseData), typeof(TurretUIData))]
public partial class TurretConfig { }

// Source Generator 自动生成：
// - TurretConfigPackage 类
// - GetTurretAsync 方法
// - TurretConfigEditor 类
// - TurretId 枚举
```

### 6.3 低风险改进建议

#### 建议 1: 添加性能监控

```csharp
public class EntityDataMetrics
{
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long TotalLoadTimeMs { get; set; }
    public int CurrentCacheSize => _cache.Count;

    public double CacheHitRate =>
        (double)CacheHits / (CacheHits + CacheMisses);

    public double AverageLoadTimeMs =>
        CacheMisses > 0 ? (double)TotalLoadTimeMs / CacheMisses : 0;
}
```

#### 建议 2: 添加配置验证工具

```csharp
public class EntityDataValidator
{
    public ValidationResult ValidateAll()
    {
        var result = new ValidationResult();

        foreach (var package in _allPackages)
        {
            // 验证必填字段
            if (string.IsNullOrEmpty(package.EntityIdToken))
                result.AddError(package, "EntityIdToken is required");

            // 验证数值范围
            if (package is TurretConfigPackage turret && turret.Base.Cost < 0)
                result.AddError(package, "Cost cannot be negative");

            // 验证资源引用
            if (!string.IsNullOrEmpty(package.EntityBlueprintGuid))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EntityBlueprint>(
                    AssetDatabase.GUIDToAssetPath(package.EntityBlueprintGuid));
                if (asset == null)
                    result.AddWarning(package, $"Blueprint not found: {package.EntityBlueprintGuid}");
            }
        }

        return result;
    }
}
```

---

## 7. 结论与建议

### 7.1 总体评价

EntityData 架构是一个设计良好、实现完整的实体数据管理系统。它成功平衡了类型安全、性能和开发效率的需求，特别适合中小型 Unity 项目。

**核心优势**:
1. 强类型设计带来编译期安全保障
2. MemoryPack 提供高性能序列化
3. 完整的热更新和版本管理支持
4. 丰富的编辑器工具链

**主要限制**:
1. 添加新实体类型工作量较大
2. 索引查找性能随实体数量线性下降
3. 缓存无上限，有 OOM 风险

### 7.2 优先级建议

#### P0（必须）- 立即修复
- 无（架构整体健康）

#### P1（高优先级）- 近期优化
1. **实现字典缓存优化索引查找**（风险 2）
   - 预计收益：查找性能从 O(n) 提升到 O(1)
   - 工作量：~2 小时

2. **添加缓存容量限制**（风险 1）
   - 预计收益：防止长时间运行 OOM
   - 工作量：~4 小时

#### P2（中优先级）- 中期规划
3. **泛型化重构减少重复代码**（风险 3）
   - 预计收益：添加新类型工作量减少 70%
   - 工作量：~2-3 天
   - 风险：重构引入回归 bug

4. **添加性能监控和验证工具**
   - 预计收益：提升问题诊断效率
   - 工作量：~1 天

#### P3（低优先级）- 长期考虑
5. Source Generator 自动生成代码
6. 分布式配置加载（支持大规模配置）
7. 配置数据压缩

### 7.3 针对不同项目规模的建议

| 项目规模 | 实体数量 | 建议 |
|---------|---------|-----|
| 小型 (<50) | <100 | 当前架构完全适用，无需优化 |
| 中型 (50-200) | 100-500 | 建议实现 P1 优化项 |
| 大型 (200+) | 500+ | 必须实现 P1 + P2 优化项，考虑架构重构 |

---

**报告结束**

**附录**:
- 附录 A: 性能测试基准代码（建议补充）
- 附录 B: 内存分析工具使用指南（建议补充）
- 附录 C: 迁移指南（如实施 P2 重构，需补充）
