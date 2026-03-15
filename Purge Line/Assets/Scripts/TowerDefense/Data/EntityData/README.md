# EntityData 模块说明

## 内容
- 运行时：`EntityDataService` + `EntityAddressIndex` + `EntityConfigPackage`。
- 编辑器：`EntityDataHubWindow` + `SingleEntityConfigEditorAsset` + `EntityIdEnumGenerator`。
- 测试：`Assets/Scripts/TowerDefense/Tests/EntityData*.cs`。

## 快速试用
1. 菜单打开 `PurgeLine/Entity Data Hub`。
2. 在分类中点击 `新增配置包`。
3. 点击 `编辑`，在单体编辑器里填写数据并 `保存`。
4. 运行时通过 `IEntityDataService.GetAsync(EntityId)` 获取配置。

## 注意
- `EntityId.g.cs` 为自动生成文件，请勿手改。
- Addressables address 由编辑器工具自动维护。
- 蓝图引用仅存 GUID，不通过 Addressables 读取。

