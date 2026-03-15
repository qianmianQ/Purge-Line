# EntityData 深度重构计划（本轮）

## 目标清单
- [x] `EntityDataService` 注入 `GameLifetimeScope`（VContainer）。
- [x] 中枢窗口：按名称创建ID；地址后缀与枚举名从名称自动生成。
- [x] 中枢窗口：删除前引用检查；单项校验；全量校验；全局/分类折叠展开；刷新。
- [x] ID体系改为按 `EntityType` 分枚举（`TurretId/EnemyId/ProjectileId`，各自 `Max`）。
- [x] `EntityType` 增加 `Max`。
- [x] 数据模型按类型拆分：各类型独立 `BaseData`/`UIData`/`ConfigPackage`，保留通用兼容包。
- [x] 单体编辑SO按类型拆分（炮塔/敌人/子弹独立编辑器）。
- [x] 蓝图：新建后自动打开新蓝图；新增“编辑实体蓝图”；组件库全部折叠/展开；蓝图名编辑与保存重载。

## 架构改造点
1. 数据层树状结构
   - `EntityAddressIndex`（中枢）
   - `EntityTypeAddressBucket`（分类）
   - `EntityAddressItem`（实体节点，`LocalId + EnumName + Token + Address`）
2. 运行时访问
   - `IEntityDataService.GetTurretAsync(TurretId)`
   - `IEntityDataService.GetEnemyAsync(EnemyId)`
   - `IEntityDataService.GetProjectileAsync(ProjectileId)`
3. 兼容层
   - 通用旧结构改名为 `CommonEntityConfigCompatV1`
   - 读取失败按类型降级 fallback，不崩溃

## 关键文件
- 运行时：
  - `Assets/Scripts/TowerDefense/Data/EntityData/EntityDataContracts.cs`
  - `Assets/Scripts/TowerDefense/Data/EntityData/EntityTypedPackages.cs`
  - `Assets/Scripts/TowerDefense/Data/EntityData/EntityConfigCompatibility.cs`
  - `Assets/Scripts/TowerDefense/Data/EntityData/EntityDataService.cs`
  - `Assets/Scripts/TowerDefense/Data/EntityData/IEntityDataService.cs`
  - `Assets/Scripts/TowerDefense/Data/EntityData/Generated/EntityId.g.cs`
- 编辑器：
  - `Assets/Scripts/TowerDefense/Editor/EntityData/EntityDataHubWindow.cs`
  - `Assets/Scripts/TowerDefense/Editor/EntityData/EntityDataEditorUtility.cs`
  - `Assets/Scripts/TowerDefense/Editor/EntityData/EntityIdEnumGenerator.cs`
  - `Assets/Scripts/TowerDefense/Editor/EntityData/EntityTypedSingleEditors.cs`
  - `Assets/Scripts/TowerDefense/Editor/EntityData/SingleEntityConfigEditorAssetEditor.cs`
- DI与蓝图：
  - `Assets/Scripts/TowerDefense/Core/DI/GameLifetimeScope.cs`
  - `Assets/Scripts/TowerDefense/Editor/Blueprint/EntityBlueprintEditorWindow.cs`


