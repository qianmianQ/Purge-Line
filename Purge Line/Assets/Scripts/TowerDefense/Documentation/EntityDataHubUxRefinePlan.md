# EntityDataHub / SO 面板重构计划（本轮UI修复）

## 目标
- 修复 Hub 分类区“只显示长输入框，看不到新增按钮”的可用性问题。
- 顶部按钮固定可见：`刷新`、`全量校验`、`全部展开`、`全部折叠`。
- 全量校验输出重复 address 的具体冲突明细（type/token）。
- 保持每分类独立 SO 与独立 CustomEditor 的架构不变。

## 执行步骤
1. 重构 `EntityDataHubWindow` 顶部栏为双行布局，保证按钮可见。
2. 重构分类创建区为卡片式布局，输入与按钮分区，防止 TextField 挤压按钮。
3. 保留并强化创建校验反馈（空/重复），给出实时提示。
4. 增强 `ValidateAll` 重复 address 提示内容为 `EntityType/EntityIdToken` 明细。
5. 构建验证 `TowerDefense.Editor.csproj` 与 `TowerDefense.Tests.csproj`。
