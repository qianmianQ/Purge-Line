# Task4 - EntityDataHubWindow 编译修复

## 目标
- 修复 `IStyle` 不支持 `rowGap/columnGap` 的编译错误。
- 保持 Hub 顶部按钮和分类创建按钮可见可用。

## 修改点
- 将 `rowGap` / `columnGap` 替换为控件 `margin` 方式实现间距。
- 保留并验证以下按钮可见：
  - 顶部：`刷新` / `全量校验` / `全部展开` / `全部折叠`
  - 分类：`新增配置包`

## 结果
- `EntityDataHubWindow.cs` 编译通过。
- 不影响全量校验重复 address 明细输出逻辑。

