# GridModificationSystem 详解

## 1. 系统定位与职责

`GridModificationSystem` 是塔防游戏中负责管理地图格子状态的 ECS 系统。它主要负责：
- 炮塔放置：标记格子为已占据，记录占据者 Entity
- 炮塔移除：释放格子占据状态
- 格子状态查询：提供 API 供其他系统调用

该系统运行于 Unity ECS 的 `SimulationSystemGroup`，即主游戏循环。

## 2. 主要成员与生命周期

- `OnCreate`：初始化日志器，要求 `GridMapData` 存在。
- `OnDestroy`：销毁时记录日志。
- `OnUpdate`：当前为空，被动系统，仅在有请求时被调用。

## 3. 核心 API 方法

### 3.1 TryPlaceTower

- 检查边界、格子类型、占据状态。
- 若可放置，则更新格子状态为占据，并记录炮塔 Entity。
- 标记地图为脏（添加 `GridDirtyTag`）。

### 3.2 TryRemoveTower

- 检查边界、占据状态。
- 若有炮塔，则清空格子状态。
- 标记地图为脏。

### 3.3 TryGetCellState

- 查询指定格子的类型与状态。
- 返回格子类型（如可放置、可通行等）和当前占据信息。

### 3.4 TryGetCellAtWorldPos

- 根据世界坐标转换为格子坐标，查询格子信息。

### 3.5 CanPlaceAt / IsWalkableAt

- 判断格子是否可放置炮塔或可通行。

## 4. 内部工具方法

- TryGetMapDataAndBuffer：查找地图数据和格子状态 buffer。

## 5. 数据流与交互

- 格子状态由 `GridCellState` buffer 管理。
- 地图数据由 `GridMapData` 管理。
- 放置/移除操作会标记地图为脏，触发后续系统响应。

## 6. 设计思路与扩展

- 被动系统，主线程调用 API，后续可扩展为处理请求 buffer。
- 日志详细，便于调试。
- 支持边界检查、类型检查、占据检查，保证操作安全。

## 7. 典型调用流程

1. 放置炮塔：`TryPlaceTower` → 检查 → 更新状态 → 标记脏
2. 移除炮塔：`TryRemoveTower` → 检查 → 清空状态 → 标记脏
3. 查询格子：`TryGetCellState` / `TryGetCellAtWorldPos`

## 8. 与其他系统的关系

- 供塔防核心逻辑、路径规划、UI等系统调用。
- 保证地图格子状态一致性。

---

后续可继续讲解其他系统或脚本，如需详细代码逐行分析，请指定具体文件。
