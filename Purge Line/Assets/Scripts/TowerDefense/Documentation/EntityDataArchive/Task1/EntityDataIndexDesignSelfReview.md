# 分层地址索引式实体数据管理系统设计方案自审

## 覆盖性检查
- [x] 顶层 `EntityType -> address集合` 分层索引
- [x] 类型层 `EntityId -> address` 映射
- [x] `EntityId` 自动代码生成
- [x] `BaseData` / `UIData` / `EntityBlueprintGuid` / 扩展地址字段
- [x] 逻辑层仅依赖 `EntityId`
- [x] 运行时反向查询（instance -> id + package）
- [x] 热更与 Buff 修改触发变更事件
- [x] Addressables 加载失败容错
- [x] MemoryPack + VersionTolerant + 跳跃编号
- [x] 旧版本降级读取策略
- [x] UI Toolkit 中枢窗口 + 单体 SO 编辑器
- [x] Address 自动维护，无手工输入

## 潜在漏洞检查与修正
1. **风险：`EntityId` 删除后代码引用失效**
   - 修正：中枢删除操作前做引用告警（确认弹窗），并在删除后立即触发编译刷新，尽早暴露调用点。
2. **风险：Addressables 配置缺失导致编辑器功能静默失败**
   - 修正：工具统一通过 `EntityAddressableEditorUtility` 进行创建/校验，失败时弹错误并停止保存。
3. **风险：旧版本字段缺失导致 UI 展示异常**
   - 修正：迁移后统一走 `Normalize()` 填默认值，保证运行时与编辑器都不出现 null 字段。
4. **风险：变更通知风暴**
   - 修正：事件按单 address 增量广播，不允许全量轮询；后续如出现频繁修改可加入批处理窗口。

## 结论
- 方案满足用户需求，数据边界清晰，编辑器与运行时职责分离。
- 可以进入编码阶段，并以测试覆盖增删改查、序列化兼容、热更通知、反向查询、枚举生成与编辑器流程。

