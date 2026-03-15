# EntityData 系统评估报告

> 评估日期：2026-03-15
> 评估范围：`Data/EntityData/` 运行时 + `Editor/EntityData/` 编辑器工具链
> 评估目标：配置便捷度、扩展复杂度、架构改进方案

---

## 一、现状概述

当前 EntityData 系统围绕 **EntityType 枚举**（TURRET / ENEMY / PROJECTILE）构建，每种实体类型都有独立的：

| 层次 | 每种类型需要的文件 | 示例 |
|------|-------------------|------|
| 数据包类 | `XxxConfigPackage` | `TurretConfigPackage` |
| 编辑器 SO | `XxxConfigEditorAsset` | `TurretConfigEditorAsset.cs` |
| 编辑器 Inspector | `XxxConfigEditorAssetEditor` | `TurretConfigEditorAssetEditor.cs` |
| 兼容层 switch 分支 | `EntityConfigCompatibility` 中的 case | `case EntityType.TURRET:` |
| Service 加载方法 | `GetXxxAsync()` | `GetTurretAsync(TurretId)` |
| 验证规则 | `EntityDataValidator` 中的 case | 类型特定校验逻辑 |
| 生成的枚举 | `XxxId` | `TurretId { None, ASAJIJID, TEST }` |
| SingleEditor 资产 | SO 实例 | `TURRET_SingleEditor.asset` |

**总计**：新增一种实体类型至少需要触及 **6-8 个文件**，编写约 **200-400 行**样板代码。

---

## 二、配置便捷度评估

### 2.1 当前工作流（以配置一个新炮塔为例）

```
1. EntityDataHub 窗口 → 输入名称 → 创建
2. 自动生成 .bytes 文件、注册表记录、枚举
3. 打开 SingleEditor Inspector
4. 填写字段：Name, Cost, MaxHp, AttackRange, AttackInterval...
5. 填写 UI 字段：DisplayName, Description, IconAddress...
6. 创建/选择 Blueprint → 编译
7. 自动保存 → 完成
```

### 2.2 优点

| 项目 | 说明 |
|------|------|
| 类型安全 | MemoryPack + 强类型枚举，编译期发现错误 |
| 自动化程度 | 枚举自动生成、地址自动注册、索引自动重建 |
| 性能 | MemoryPack 二进制序列化 + LRU 缓存，运行时高效 |
| 数据完整性 | Preflight 检查（播放前 + 构建前），防止无效数据进入运行时 |
| 版本兼容 | `VersionTolerant` + `SchemaVersion`，支持前向兼容 |

### 2.3 痛点

#### 痛点 1：字段重复，改一个字段要改多处

三种类型都有完全相同的 UI 字段组（DisplayName, Description, IconAddress, PreviewAddress, ThemeColorHex），但各自独立定义。
修改或新增一个 UI 相关字段需要同时修改 **3 个 ConfigPackage 类** + **3 个 Inspector 类**。

```
影响文件数：6 个
出错风险：高（遗漏某个类型的修改）
```

#### 痛点 2：Inspector 编辑体验受限

当前 Inspector 是手写 IMGUI 代码，每个类型的编辑器都是手动排列 `EditorGUILayout` 调用。
- 无法拖拽排序字段
- 无法动态显示/隐藏字段
- 无法做字段间联动（如选了某种攻击类型后显示对应参数）
- 添加新字段需要同时在 **Package 类** 和 **Inspector 类** 两处修改

#### 痛点 3：缺乏数据模板/继承机制

如果 10 个敌人都是 100 HP、只是速度和奖励不同，需要手动逐个设置 HP。
没有"基础模板 → 覆盖差异"的机制，重复劳动多。

#### 痛点 4：Blueprint 与 ConfigPackage 的职责边界模糊

- ConfigPackage 存储数值数据（HP、速度等）
- Blueprint 存储 ECS Component 组合
- 两者都描述"一个实体是什么"，但分开管理，关系靠地址字符串链接
- 编辑流程中需要在两个编辑器之间跳转

---

## 三、扩展复杂度评估

### 3.1 新增实体类型的完整步骤

假设要新增 `BUFF`（增益效果）类型：

```
步骤 1: EntityType.cs
   → 添加 BUFF = 3, 修改 Max = 4

步骤 2: EntityTypedPackages.cs
   → 新建 BuffConfigPackage 类（~80行）
   → 添加 [MemoryPackable] 标注
   → 定义所有字段 + FieldOrder 枚举
   → 实现 IEntityConfigPackage 接口
   → 实现 BuildFallback() 和 Normalize()

步骤 3: BuffConfigEditorAsset.cs（新文件）
   → 继承 EntityConfigEditorAssetBase
   → 持有 BuffConfigPackage 字段
   → 实现 GetPackage() / SetPackage()

步骤 4: BuffConfigEditorAssetEditor.cs（新文件）
   → 继承 EntityConfigEditorInspectorBase
   → 重写 DrawTypeSpecificFields()
   → 手写所有字段的 IMGUI 布局代码

步骤 5: EntityConfigCompatibility.cs
   → 添加 case EntityType.BUFF 的反序列化分支

步骤 6: EntityDataService.cs
   → 添加 GetBuffAsync(BuffId id) 方法

步骤 7: EntityDataValidator.cs
   → 添加 BUFF 类型的校验规则

步骤 8: EntityTypedSingleEditors.cs
   → 添加 BuffSingleEditorAsset 类

步骤 9: EntityDataEditorUtility.cs
   → 确认 CreateNewRecord 和其他方法兼容新类型
   → 可能需要修改 Hub 窗口中的类型过滤逻辑

步骤 10: 手动创建 Editor 资产
   → 创建 BUFF_SingleEditor.asset
   → 创建 Configs/buff/ 目录
```

**评估**：
- 步骤数：10 步
- 新增文件：2-3 个
- 修改文件：5-6 个
- 代码量：约 200-400 行（大部分是样板代码）
- 出错风险：中高（遗漏某个 switch case、忘记注册等）
- 需要的知识：需要了解整个系统的完整架构

### 3.2 新增字段的步骤

假设要给炮塔添加"暴击率"字段：

```
步骤 1: TurretConfigPackage → 添加字段 + FieldOrder 条目
步骤 2: TurretConfigEditorAssetEditor → 添加 IMGUI 渲染代码
步骤 3: EntityDataValidator → 添加验证规则（可选）
步骤 4: 增加 SchemaVersion（可选，但推荐）
```

**评估**：4 步，但仍需手动保持 Package 和 Editor 的同步。

### 3.3 复杂度增长曲线

```
实体类型数 │ 需维护的文件数  │ 新增类型的工作量
─────────┼──────────────┼──────────────
    3     │     ~25      │   ~300 行
    5     │     ~35      │   ~300 行（线性增长）
   10     │     ~60      │   ~300 行（但认知负担指数增长）
   20     │    ~110      │   ~300 行（维护成本不可控）
```

核心问题：**每种类型的样板代码量恒定，但认知负担和一致性维护成本随类型数增长而加速上升**。

---

## 四、架构改进方案

### 方案 A：最小侵入式优化（在现有架构上改进）

**核心思路**：消除重复代码，不改变整体架构。

#### A.1 提取公共字段到基类/接口

```csharp
// 将重复的 UI 字段提取为可复用块
[MemoryPackable]
public partial class EntityUIData
{
    public string DisplayName;
    public string Description;
    public string IconAddress;
    public string PreviewAddress;
    public string ThemeColorHex;
}

// TurretConfigPackage 变为组合结构
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class TurretConfigPackage : IEntityConfigPackage
{
    [MemoryPackOrder(100)] public EntityUIData UIData;  // 复用
    [MemoryPackOrder(0)]   public int Cost;             // 类型特有
    [MemoryPackOrder(1)]   public float MaxHp;
    // ...
}
```

**影响**：
- 减少约 30% 字段重复
- 修改 UI 字段只需改 1 处
- 但 Inspector 代码仍需手写

#### A.2 反射驱动的 Inspector 自动生成

```csharp
// 通过属性标注驱动 Inspector 自动绘制
[ConfigField("造价", group: "基础属性", min: 0)]
[MemoryPackOrder(0)]
public int Cost;

[ConfigField("最大生命值", group: "基础属性", min: 0.01f)]
[MemoryPackOrder(1)]
public float MaxHp;

[ConfigField("图标", group: "UI", fieldType: ConfigFieldType.SpriteAddress)]
[MemoryPackOrder(100)]
public string IconAddress;
```

```csharp
// 通用 Inspector 基类，通过反射自动绘制
public class AutoEntityConfigInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var package = GetPackage();
        foreach (var field in ReflectConfigFields(package.GetType()))
        {
            DrawFieldByAttribute(field, package);
        }
    }
}
```

**影响**：
- 新增类型 **不再需要手写 Inspector 代码**
- 消除了最大的样板来源（每个 Inspector 约 80-120 行）
- 代价：反射有轻微性能开销（仅 Editor 中，可接受）

#### A.3 泛型 EditorAsset 消除 SO 样板

```csharp
// 替代 TurretConfigEditorAsset / EnemyConfigEditorAsset / ProjectileConfigEditorAsset
// 用一个泛型 + SerializeReference 方案
public class GenericEntityConfigEditorAsset : EntityConfigEditorAssetBase
{
    [SerializeReference]
    private IEntityConfigPackage _package;

    public override IEntityConfigPackage GetPackage() => _package;
    public override void SetPackage(IEntityConfigPackage p) => _package = p;
}
```

**影响**：
- 新增类型 **不再需要新建 EditorAsset 类**
- 一个 SO 类适配所有实体类型
- 3 个 EditorAsset + 3 个 Inspector → 1 个 EditorAsset + 1 个自动 Inspector

#### 方案 A 总结

| 改进项 | 消除的样板 | 侵入性 |
|--------|-----------|--------|
| A.1 提取 UIData | 字段重复 | 低 |
| A.2 反射 Inspector | Inspector 代码 | 中 |
| A.3 泛型 EditorAsset | SO 类 | 中 |

**新增类型的步骤缩减**：10 步 → 4 步
```
1. EntityType 枚举添加值
2. 新建 XxxConfigPackage（用属性标注字段）
3. EntityConfigCompatibility 添加 case
4. EntityDataService 添加加载方法
```

不再需要：手写 EditorAsset、手写 Inspector、手写 SingleEditor

---

### 方案 B：组合式配置架构（中度重构）

**核心思路**：将单体 ConfigPackage 拆分为可组合的"配置组件"，新类型通过组合已有组件来定义。

#### B.1 架构设计

```
EntityConfig（运行时容器）
├── BaseInfoBlock      （所有类型共有：Name, Description）
├── UIBlock            （所有类型共有：DisplayName, Icon, Preview, Color）
├── CombatBlock        （战斗类型：MaxHp, AttackRange, AttackInterval）
├── MovementBlock      （移动类型：MoveSpeed）
├── ProjectileBlock    （弹药类型：Speed, LifeTime, Damage）
├── EconomyBlock       （经济类型：Cost 或 Reward）
└── CustomBlock        （自定义 KV 扩展）
```

```csharp
// 配置组件接口
public interface IConfigBlock
{
    string BlockId { get; }        // "combat", "movement" 等
    void Normalize();
    ValidationResult Validate();
}

// 实体配置 = 组件的集合
[MemoryPackable(GenerateType.VersionTolerant)]
public partial class EntityConfig : IEntityConfigPackage
{
    [MemoryPackOrder(0)] public EntityType EntityType;
    [MemoryPackOrder(1)] public string EntityIdToken;
    [MemoryPackOrder(2)] public int Version;

    // 可选组件块
    [MemoryPackOrder(10)] public BaseInfoBlock BaseInfo;
    [MemoryPackOrder(11)] public UIBlock UI;
    [MemoryPackOrder(12)] public CombatBlock Combat;      // null = 此类型不需要
    [MemoryPackOrder(13)] public MovementBlock Movement;   // null = 此类型不需要
    [MemoryPackOrder(14)] public ProjectileBlock Projectile;
    [MemoryPackOrder(15)] public EconomyBlock Economy;
}
```

#### B.2 类型定义通过"模板"声明

```csharp
// 不再硬编码 switch case，改为注册制
public static class EntityTypeTemplates
{
    public static readonly Dictionary<EntityType, Type[]> RequiredBlocks = new()
    {
        [EntityType.TURRET] = new[] {
            typeof(BaseInfoBlock), typeof(UIBlock),
            typeof(CombatBlock), typeof(EconomyBlock)
        },
        [EntityType.ENEMY] = new[] {
            typeof(BaseInfoBlock), typeof(UIBlock),
            typeof(MovementBlock), typeof(EconomyBlock)
        },
        [EntityType.PROJECTILE] = new[] {
            typeof(BaseInfoBlock), typeof(UIBlock),
            typeof(ProjectileBlock)
        },
        // 新增 BUFF 类型只需在这里添加一行
        [EntityType.BUFF] = new[] {
            typeof(BaseInfoBlock), typeof(UIBlock),
            typeof(BuffBlock)
        },
    };
}
```

#### B.3 Editor 自动适配

```csharp
// Inspector 根据类型模板自动渲染对应的 Block
public class EntityConfigInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var config = target as EntityConfig;
        var requiredBlocks = EntityTypeTemplates.RequiredBlocks[config.EntityType];

        foreach (var blockType in requiredBlocks)
        {
            var block = config.GetBlock(blockType);
            DrawBlockFoldout(block);  // 每个 Block 自带绘制逻辑或用反射
        }
    }
}
```

#### B.4 新增类型的步骤

```
1. EntityType 枚举添加值
2. （如果需要新 Block）定义 XxxBlock 类（~20行）
3. EntityTypeTemplates 注册一行
完成。
```

**从 10 步缩减到 1-3 步，且不需要任何 Editor 代码。**

#### 方案 B 总结

| 维度 | 现状 | 方案 B |
|------|------|--------|
| 新增类型步骤 | 10 步，6-8 个文件 | 1-3 步，0-1 个新文件 |
| 新增字段 | 改 Package + Inspector | 改 Block 即可，Inspector 自动适配 |
| 类型间共享字段 | 手动重复定义 | Block 天然复用 |
| Inspector 维护 | 每类型手写 | 零维护，自动生成 |
| MemoryPack 兼容 | VersionTolerant 已支持 | 继续使用，Block 级别 VersionTolerant |
| 迁移成本 | — | 中等（需重构序列化层 + 迁移现有数据） |

---

### 方案 C：Schema-Driven 全数据驱动（大幅重构）

**核心思路**：用数据文件（YAML/JSON/ScriptableObject）定义实体类型的 schema，运行时和编辑器都从 schema 驱动。

#### C.1 架构概念

```
Schema 定义（编辑器时）
┌────────────────────────────────┐
│ turret_schema.yaml             │
│   fields:                      │
│     - name: Cost               │
│       type: int                │
│       min: 0                   │
│       group: "基础属性"         │
│     - name: MaxHp              │
│       type: float              │
│       min: 0.01                │
│       group: "战斗"            │
│     - name: IconAddress        │
│       type: sprite_address     │
│       group: "UI"              │
└────────────────────────────────┘
           ↓ 编辑器读取 schema
┌────────────────────────────────┐
│ Auto-generated Inspector       │
│ [基础属性]                      │
│   Cost: [____0____]            │
│ [战斗]                         │
│   MaxHp: [__100.0__]           │
│ [UI]                           │
│   Icon: [Select Sprite]       │
└────────────────────────────────┘
           ↓ 保存
┌────────────────────────────────┐
│ turret_xxx.bytes               │
│ (Dictionary<string, object>    │
│  序列化为 MemoryPack)          │
└────────────────────────────────┘
```

#### C.2 优劣势

**优势**：
- 新增类型 = 新建一个 schema 文件，**零代码**
- 新增字段 = schema 文件加一行，**零代码**
- 策划可以自己定义新实体类型（如果提供 schema 编辑器）
- Inspector 100% 自动生成

**劣势**：
- **失去编译期类型安全**：运行时通过字符串键访问字段，拼写错误不会在编译时报错
- **性能退化**：字典查找 vs 直接字段访问
- **调试困难**：数据结构不再是静态可知的
- **MemoryPack 兼容性差**：MemoryPack 需要编译期已知类型，字典方案需要自定义序列化
- **与 ECS 集成复杂**：ECS 需要 blittable struct，动态 schema 不易映射到 ECS 组件
- **迁移成本极高**：几乎是重写

#### C.3 适用场景判断

Schema-Driven 更适合：
- 大型 RPG 的物品/技能系统（数百种类型、策划主导）
- 数据量远大于代码逻辑的场景
- 团队中策划需要独立定义新类型

**不太适合 Purge Line 的场景**：
- 塔防实体类型数量有限（预计 < 20 种）
- 每种类型有明确的游戏逻辑差异（不是简单的数值差异）
- ECS 运行时需要类型安全

---

## 五、方案对比与推荐

### 综合对比

| 评估维度 | 现状 | 方案 A（最小优化） | 方案 B（组合式） | 方案 C（全数据驱动） |
|---------|------|-------------------|-----------------|---------------------|
| 新增类型步骤 | 10 步 | 4 步 | 1-3 步 | 0 步（纯数据） |
| 新增字段步骤 | 2-4 步 | 1-2 步 | 1 步 | 1 步（纯数据） |
| 类型安全 | ★★★★★ | ★★★★★ | ★★★★☆ | ★★☆☆☆ |
| 配置便捷度 | ★★★☆☆ | ★★★★☆ | ★★★★★ | ★★★★★ |
| 运行时性能 | ★★★★★ | ★★★★★ | ★★★★☆ | ★★★☆☆ |
| ECS 集成 | ★★★★★ | ★★★★★ | ★★★★☆ | ★★☆☆☆ |
| 迁移成本 | — | 低 | 中 | 高 |
| 长期维护成本 | 高 | 中 | 低 | 低 |
| 学习成本 | 高 | 中 | 低 | 中 |

### 推荐路线

#### 如果实体类型预计 ≤ 10 种：方案 A（最小优化）

投入小、风险低，足以解决当前痛点。重点做 A.2（反射 Inspector）和 A.3（泛型 EditorAsset），可在 1-2 天内完成。

#### 如果实体类型预计 10-30 种，或字段变动频繁：方案 B（组合式）✅ 推荐

这是**性价比最高**的方案：
- 保留 MemoryPack 的性能优势和类型安全
- 通过 Block 组合消除绝大部分样板代码
- Inspector 自动化程度高
- 与现有 Blueprint 系统自然对接
- 可以渐进式迁移（先迁移新类型，旧类型逐步迁移）

#### 如果是大型 RPG / 策划主导定义实体：方案 C（全数据驱动）

当前项目可能不需要走到这一步，但如果将来游戏类型变化，可以考虑。

---

## 六、方案 B 实施概要（推荐方案）

### 第一阶段：基础设施（不破坏现有功能）

```
1. 定义 IConfigBlock 接口 + ConfigFieldAttribute
2. 实现公共 Block：BaseInfoBlock, UIBlock, EconomyBlock
3. 实现通用反射 Inspector（AutoBlockInspector）
4. 验证 MemoryPack 对 Block 嵌套的兼容性
```

### 第二阶段：统一容器

```
1. 创建 EntityConfig 统一容器类
2. 实现 EntityTypeTemplate 注册机制
3. 统一 EntityDataService 的加载逻辑（消除 per-type 方法）
4. 统一 EntityConfigCompatibility 的反序列化
```

### 第三阶段：迁移现有数据

```
1. 编写迁移工具：旧 XxxConfigPackage → 新 EntityConfig
2. 批量转换现有 .bytes 文件
3. 验证运行时加载正确性
4. 清理旧的 per-type 代码
```

### 第四阶段：增强体验

```
1. 添加模板/预设机制（从模板创建新实体，只覆盖差异值）
2. 添加批量编辑功能（选中多个实体同时修改某字段）
3. 添加数据对比/diff 功能
```

---

## 七、关于 Blueprint 系统的整合思考

当前 ConfigPackage 和 Blueprint 是分离的两套数据：
- **ConfigPackage**：存储游戏数值（HP、速度、造价）
- **Blueprint**：存储 ECS Component 组合（Transform、Sprite、碰撞体）

这种分离在当前架构下是合理的——它们的变更频率和使用者不同（数值由策划调整，组件组合由程序定义）。

但在方案 B 中，可以考虑让 Blueprint 也成为 EntityConfig 的一个 Block：

```csharp
[EntityType.TURRET] = new[] {
    typeof(BaseInfoBlock),      // 基础信息
    typeof(UIBlock),            // UI 展示
    typeof(CombatBlock),        // 战斗数值
    typeof(EconomyBlock),       // 经济数值
    typeof(BlueprintRefBlock),  // Blueprint 引用（编译地址 + hash）
}
```

这样 Inspector 中 Blueprint 区域也会自动渲染，无需额外编辑器代码。

---

## 八、结论

### 当前系统的核心问题

1. **样板代码比例过高**：每种类型需要 6-8 个文件，其中 ~60% 是结构性重复
2. **Inspector 与数据模型耦合**：每个字段在两处定义（Package + Inspector），违反 DRY
3. **扩展时修改点分散**：新增类型需要修改 5-6 个现有文件中的 switch/case

### 建议采取行动

**短期**（如果不想大改）：实施方案 A.2 + A.3，用反射 Inspector + 泛型 EditorAsset 消除最痛的样板代码。

**中期**（如果计划扩展更多实体类型）：实施方案 B，将 ConfigPackage 拆分为可组合的 Block，实现真正的"添加一种新类型 = 声明它由哪些 Block 组成"的体验。

两个方案都不需要抛弃现有的 MemoryPack 序列化和 Addressables 加载机制，改动是渐进式的。
