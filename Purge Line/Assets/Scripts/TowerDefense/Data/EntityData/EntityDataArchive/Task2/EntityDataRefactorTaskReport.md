# EntityData 重构任务报告

## 1. 任务结果概览
- 状态：已完成本轮核心重构与功能增强。
- 编译验证：`dotnet test TowerDefense.Tests.csproj` 构建通过。
- 交付维度：架构重构、编辑器增强、蓝图交互优化、DI接入、测试更新、文档更新。

## 2. 已完成项（对应需求）
1) VContainer 注入
- `GameLifetimeScope` 注入 `IEntityDataService`。

2) 中枢窗口工业化增强
- 按名称输入创建ID（名称驱动 token/address/枚举名）。
- 删除前引用扫描阻断。
- 单项校验与全量校验（重复 address 检测）。
- 全局/分类折叠展开。
- 刷新重读。

3) 深度重构（树状层级）
- 枚举体系拆分：`TurretId/EnemyId/ProjectileId` + `Max`。
- `EntityType` 增加 `Max`。
- 类型独立包体与数据结构：
  - `TurretConfigPackage + TurretBaseData + TurretUIData`
  - `EnemyConfigPackage + EnemyBaseData + EnemyUIData`
  - `ProjectileConfigPackage + ProjectileBaseData + ProjectileUIData`
- 通用兼容包重命名：`CommonEntityConfigCompatV1`。
- 索引模型：`EntityAddressIndex -> EntityTypeAddressBucket -> EntityAddressItem`。

4) 单体SO编辑器按类型拆分
- `TurretConfigEditorAsset`
- `EnemyConfigEditorAsset`
- `ProjectileConfigEditorAsset`

5) 蓝图编辑器增强
- 新建蓝图后自动打开并加载。
- 新增“编辑实体蓝图”。
- 组件库“全部展开/全部折叠”。
- 新增蓝图名编辑框并支持改名保存重载。

## 3. 风险与后续建议
- 当前校验规则聚焦 address/文件存在性/基础字段；后续可增加字段语义校验（例如数值区间）。
- 删除引用扫描目前覆盖 icon/preview/sfx，可继续扩展到后续新增地址字段。
- 如果运行时要强制预热，可新增启动器在场景加载后调用 `InitializeAsync`。

## 4. 关键文件清单
- `Assets/Scripts/TowerDefense/Core/DI/GameLifetimeScope.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/EntityType.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/Generated/EntityId.g.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/EntityDataContracts.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/EntityTypedPackages.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/EntityConfigCompatibility.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/IEntityDataService.cs`
- `Assets/Scripts/TowerDefense/Data/EntityData/EntityDataService.cs`
- `Assets/Scripts/TowerDefense/Editor/EntityData/EntityDataEditorUtility.cs`
- `Assets/Scripts/TowerDefense/Editor/EntityData/EntityDataHubWindow.cs`
- `Assets/Scripts/TowerDefense/Editor/EntityData/EntityIdEnumGenerator.cs`
- `Assets/Scripts/TowerDefense/Editor/EntityData/EntityTypedSingleEditors.cs`
- `Assets/Scripts/TowerDefense/Editor/EntityData/SingleEntityConfigEditorAssetEditor.cs`
- `Assets/Scripts/TowerDefense/Editor/Blueprint/EntityBlueprintEditorWindow.cs`


