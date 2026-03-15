# 分层地址索引式实体数据管理系统设计方案

## 1. 目标与边界
- 目标：建立一套仅以 `EntityId` 作为逻辑入口的实体数据系统，运行时通过 Addressables + MemoryPack 加载，编辑器自动维护地址、索引与枚举。
- 边界：
  - 逻辑层**禁止直接持有 address**。
  - ScriptableObject 仅用于编辑器态“单个实体配置编辑器”，不参与运行时。
  - 实体行为蓝图仅存 GUID，不使用 Addressables 加载。

## 2. 分层架构图

```text
+----------------------------------------------------------------------------------+
| Logic / Gameplay Layer                                                           |
|----------------------------------------------------------------------------------|
| 只使用 EntityId (enum)                                                           |
+------------------------------------+---------------------------------------------+
                                     |
                                     v
+----------------------------------------------------------------------------------+
| Runtime Data Layer                                                               |
|----------------------------------------------------------------------------------|
| IEntityDataService                                                               |
|  - LoadIndexAsync() -> EntityAddressIndex                                        |
|  - GetAsync(EntityId) -> EntityConfigPackage                                     |
|  - RegisterRuntimeInstance(instance, id)                                         |
|  - ApplyRuntimeMutation(id, mutator) + 变更事件                                  |
+---------------------------+------------------------------+-----------------------+
                            |                              |
                            v                              v
                  +--------------------+         +-----------------------------+
                  | Addressables       |         | ReverseLookup               |
                  | index + package    |         | instance -> (id, package)   |
                  +--------------------+         +-----------------------------+

+----------------------------------------------------------------------------------+
| Editor Data Pipeline                                                             |
|----------------------------------------------------------------------------------|
| EntityDataHubWindow(UI Toolkit)                                                  |
|   -> EntityConfigRegistryAsset(编辑器索引)                                       |
|   -> 自动生成 EntityId.g.cs                                                      |
|   -> 自动生成/更新 entity_index.bytes                                            |
|   -> 自动维护 Addressables address                                                |
| SingleEntityConfigEditorAsset + CustomEditor                                     |
|   -> 编辑单个 EntityConfigPackage                                                 |
|   -> 保存/加载 .bytes + 蓝图 GUID                                                 |
+----------------------------------------------------------------------------------+
```

## 3. 数据流流程图

```text
[新增配置包]
  -> HubWindow 生成唯一 EntityId Token
  -> 创建默认 EntityConfigPackage
  -> MemoryPack 序列化到 Assets/Data/EntityData/<type>/<id>.bytes
  -> 自动设置 Addressables Address: td/entity/<type>/<id>
  -> 更新编辑器注册表
  -> 自动生成 EntityId.g.cs
  -> 重建 entity_index.bytes (address: td/entity/index)

[运行时读取]
  EntityId -> IEntityDataService.GetAsync(id)
          -> 索引表映射 address
          -> Addressables.Load<TextAsset>(address)
          -> MemoryPack 反序列化 + 兼容迁移
          -> 缓存并返回 EntityConfigPackage

[热更/运行时 Buff 修改]
  -> NotifyHotUpdate(address) 或 ApplyRuntimeMutation(id)
  -> package.Version++ / package.IsDirty=true
  -> 发布 OnEntityDataChanged(address, id, version)
  -> 监听方按事件增量刷新（禁止全量轮询）
```

## 4. 核心类型定义

### 4.1 枚举
```csharp
public enum EntityType
{
    TURRET = 0,
    ENEMY = 1,
    PROJECTILE = 2
}

// 由编辑器自动生成，不允许手改
public enum EntityId
{
    NONE = 0,
    TURRET_WIND_SENTRY = 1,
    ENEMY_GOBLIN = 2,
    PROJECTILE_WIND_BOLT = 3
}
```

### 4.2 核心数据模型
- `EntityAddressIndex`：顶层类型注册 + 类型内 ID->address 映射。
- `EntityConfigPackage`：实体配置包（MemoryPack）。
  - `BaseData`（内嵌）：基础数值字段。
  - `UIData`（内嵌）：名称/描述/图标地址/预览图地址/主题色。
  - `EntityBlueprintGuid`：行为蓝图 GUID（.entitybp）。
  - `ExtraSfxAddress` 等扩展资源地址字段。
  - `Version` + `IsDirty`。

## 5. MemoryPack 序列化编号规则
- 所有可序列化类型统一：`[MemoryPackable(GenerateType.VersionTolerant)]`
- `MemoryPackOrder` 跳跃分段（不同功能段间隔 50）。

### 5.1 EntityConfigPackage 编号
- `1~20`：基础信息段（`EntityType`、`EntityIdToken`、`BaseData`）
- `50~70`：UI 段（`UIData`）
- `100~110`：蓝图引用段（`EntityBlueprintGuid`）
- `150~170`：扩展资源段（音效等地址）
- `200~220`：版本与脏标记段（`Version`、`IsDirty`）

## 6. Addressables 命名规范
- 索引地址：`td/entity/index`
- 实体配置地址：`td/entity/<entity_type_lower>/<entity_id_token_lower>`
- UI Sprite 自动地址（编辑器拖拽时）：`td/ui/sprite/<asset_name_lower>`
- 规则：
  - 小写
  - 空格转下划线
  - 非字母数字字符统一替换为 `_`

## 7. 编辑器工具交互流程

### 7.1 实体数据中枢编辑器（UI Toolkit）
```text
打开窗口 -> 按 EntityType 分类折叠展示
    -> 全局按钮: 全部展开/全部折叠
    -> 分类按钮: 新增配置包
        -> 自动生成唯一 EntityId
        -> 自动生成地址 + bytes 文件
        -> 自动更新索引 + 枚举代码
    -> 卡片按钮: 编辑/删除
        -> 编辑: 装载到对应类型单例 SO
        -> 删除: 删除 bytes + 更新索引 + 枚举移除
```

### 7.2 单个实体配置编辑器（CustomEditor）
```text
加载当前配置 -> 编辑 BaseData/UIData
拖拽 Sprite -> 自动登记 Addressables -> 写入 UIData.iconAddress/previewAddress
选择蓝图 -> 保存 GUID
新建蓝图 -> 创建 .entitybp + 打开蓝图编辑器 + 写入 GUID
保存 -> MemoryPack 序列化到 .bytes
加载 -> 从 .bytes 反序列化回填 SO
```

## 8. 枚举代码生成规则
- 输入：`EntityConfigRegistryAsset` 中全部记录。
- 输出：`Assets/Scripts/TowerDefense/Data/EntityData/Generated/EntityId.g.cs`。
- 规则：
  1. 枚举名固定 `EntityId`。
  2. 首项固定 `NONE = 0`。
  3. 项名格式：`<TYPE>_<SANITIZED_NAME>`。
  4. 名称冲突时追加 `_N`（N 从 2 开始）。
  5. 排序：按 `EntityType`、再按 `EntityIdToken` 字典序。
  6. 文件头写明 `// Auto-generated`，禁止手改。

## 9. 版本兼容方案
- 主策略：MemoryPack `VersionTolerant` + 字段分段留白。
- 降级策略：
  1. 先尝试 `EntityConfigPackage` 反序列化。
  2. 失败后尝试 `LegacyEntityConfigPackageV1`。
  3. 迁移函数映射旧字段到新结构，缺失字段填默认值。
  4. 若仍失败，返回安全降级包（`EntityIdToken=INVALID`）并打可定位错误日志（包含 id/address/异常）。

## 10. 运行时容错与事件
- Address 加载失败：不抛出到逻辑层，返回降级配置包并记录错误。
- 变更通知：`OnEntityDataChanged(EntityDataChangeEvent evt)`，按 address 粒度发布。
- 反向查询：维护 `instance -> EntityId` 绑定，并可直接返回完整缓存配置。

## 11. 扩展点
- 新实体类型：扩展 `EntityType`，HubWindow 自动新增分类。
- 新子资源字段：在 `EntityConfigPackage` 150+ 段追加字段。
- 新 UI 数据：在 `UIData` 50+ 段追加字段，不破坏旧数据读取。


