# EntityDataHub / SO 面板重构任务报告（本轮UI修复）

## 本轮完成
1. Hub 可用性修复
- 修复每分类仅见长输入框、看不到新增按钮的问题。
- 创建区改为卡片布局：标题 + 输入 + 新增按钮 + 分类折叠按钮 + 提示。
- `新增配置包` 按钮始终可见（设置最小宽度 + 行可换行）。

2. 顶部操作入口完善
- 保持并强化：`刷新`、`全量校验`、`全部展开`、`全部折叠`。
- 顶部改为双行布局，窄窗口下仍可见。

3. 全量校验明细增强
- 重复 address 报告增加冲突成员明细：`EntityType/EntityIdToken`。

4. 验证
- `dotnet build TowerDefense.Editor.csproj` 通过。
- `dotnet build TowerDefense.Tests.csproj` 通过。
- `dotnet test TowerDefense.Tests.csproj` 构建执行通过。

