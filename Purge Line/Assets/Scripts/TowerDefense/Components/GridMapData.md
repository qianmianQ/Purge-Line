# GridMapData 详解

## 1. 定义与用途

`GridMapData` 是塔防游戏地图的元数据组件，采用 Unity ECS 的 `IComponentData`，作为 Singleton 挂载在唯一 Entity 上。

## 2. 主要字段

- `Width`：地图宽度（格子数）
- `Height`：地图高度（格子数）
- `CellSize`：每个格子的世界空间边长
- `Origin`：地图左下角在世界空间的坐标
- `BlobData`：格子类型数据的 BlobAsset 引用（只读）
- `CellCount`：格子总数（Width × Height）

## 3. BlobAsset 设计

- `GridBlobData`：BlobAsset 内部结构，包含扁平化格子类型数组（`BlobArray<byte> Cells`），索引方式为 `y * Width + x`。
- BlobAsset 优势：
  - 地图格子类型数据只读，加载后不变
  - Burst/Job 友好，性能高
  - 无 chunk 碎片，内存连续

## 4. 使用场景

- 地图加载时创建并赋值
- 格子类型（如可放置、可通行等）只读查询
- 放置炮塔等操作通过 GridCellState buffer 追踪，不修改底层类型

## 5. 设计思路

- 保证地图元数据和格子类型数据只读安全
- 支持高性能 ECS Job 查询
- 便于后续扩展（如多地图、多层级）

---

如需逐行代码分析或与其他系统交互说明，请继续指定。
