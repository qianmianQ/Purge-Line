# 🧪 Performance Test Plan — Grid System

> **版本**: 1.0  
> **日期**: 2026-03-11  

---

## 1. 测试环境

| 项目 | 规格 |
|------|------|
| Unity | 2022.3 LTS |
| Entities | 1.3.15 |
| Burst | 1.8.x |
| 测试框架 | Unity Test Framework + Performance Testing Extension |
| 目标平台 | Editor (Development) + Standalone IL2CPP (Release) |

---

## 2. 测试矩阵

### 2.1 地图生成性能

| 测试用例 | 地图尺寸 | 目标 | 测量方法 |
|----------|----------|------|----------|
| 小型地图 | 10×10 | < 1ms | `Measure.Method()` |
| 中型地图 | 50×50 | < 10ms | `Measure.Method()` |
| 大型地图 | 100×100 | < 50ms | `Measure.Method()` |
| 超大地图 | 200×200 | < 100ms | `Measure.Method()` |
| 极限测试 | 500×500 | < 500ms | `Measure.Method()` |

### 2.2 格子查询性能

| 测试用例 | 操作 | 次数 | 目标 |
|----------|------|------|------|
| 坐标转换 | WorldToGrid | 10,000 | < 1ms total |
| 坐标转换 | GridToWorld | 10,000 | < 1ms total |
| 索引转换 | GridToIndex | 10,000 | < 0.5ms total |
| 边界检查 | IsInBounds | 10,000 | < 0.5ms total |
| 类型查询 | GetCellType | 10,000 | < 1ms total |
| 邻居查询 | GetNeighbors | 10,000 | < 2ms total |

### 2.3 序列化性能

| 测试用例 | 地图尺寸 | 操作 | 目标 |
|----------|----------|------|------|
| 序列化 | 200×200 | Serialize | < 5ms |
| 反序列化 | 200×200 | Deserialize | < 5ms |
| 往返一致性 | Any | Serialize → Deserialize | 数据完全一致 |
| 文件大小 | 200×200 | Binary size | < 50KB |

### 2.4 渲染性能

| 测试用例 | 地图尺寸 | 目标 |
|----------|----------|------|
| 渲染帧耗时 | 100×100 | < 1ms |
| 渲染帧耗时 | 200×200 | < 2ms |
| Draw Call 数 | 200×200 | < 50 |
| GC Allocation | Any | 0 bytes/frame |

---

## 3. 功能测试用例

### 3.1 GridMath 纯函数测试

| 编号 | 测试用例 | 描述 |
|------|----------|------|
| GM-01 | WorldToGrid_Center | 世界坐标 (0.5, 0.5) → 格子 (0, 0) |
| GM-02 | WorldToGrid_Offset | 带偏移原点的坐标转换 |
| GM-03 | WorldToGrid_Negative | 负坐标测试 |
| GM-04 | GridToWorld_Center | 格子 (0,0) → 世界坐标中心点 |
| GM-05 | GridToIndex_RowMajor | 验证行优先存储顺序 |
| GM-06 | IndexToGrid_Reverse | 索引 → 坐标，与 GridToIndex 互逆 |
| GM-07 | IsInBounds_Valid | 合法坐标返回 true |
| GM-08 | IsInBounds_OutOfRange | 超出边界返回 false |
| GM-09 | IsInBounds_Negative | 负坐标返回 false |
| GM-10 | GetCellType_Valid | 查询已知格子类型 |
| GM-11 | GetCellType_OutOfBounds | 越界查询返回 CellType.Solid |

### 3.2 LevelConfig 序列化测试

| 编号 | 测试用例 | 描述 |
|------|----------|------|
| LC-01 | RoundTrip_Basic | 基础序列化往返 |
| LC-02 | RoundTrip_AllCellTypes | 所有格子类型都能正确序列化 |
| LC-03 | RoundTrip_LargeMap | 200×200 大地图往返 |
| LC-04 | RoundTrip_EmptyMap | 空地图 (0×0) |
| LC-05 | RoundTrip_Metadata | 元数据（LevelId, Version）保持一致 |
| LC-06 | Deserialize_InvalidData | 非法数据不崩溃 |
| LC-07 | Version_Compatibility | 版本号检查 |

### 3.3 GridSpawnSystem 集成测试

| 编号 | 测试用例 | 描述 |
|------|----------|------|
| GS-01 | Spawn_CreatesSingleton | 生成后存在 GridMapData singleton |
| GS-02 | Spawn_CorrectDimensions | 尺寸正确 |
| GS-03 | Spawn_CorrectCellData | 格子数据与配置一致 |
| GS-04 | Spawn_ConsumesRequest | 请求实体被销毁 |
| GS-05 | Spawn_BufferCreated | GridCellState buffer 已创建 |
| GS-06 | Spawn_ReplacesExisting | 重复生成替换旧数据 |

### 3.4 Grid Modification 测试

| 编号 | 测试用例 | 描述 |
|------|----------|------|
| MD-01 | PlaceTower_OccupiesCell | 放置炮塔后格子标记占据 |
| MD-02 | RemoveTower_FreesCell | 移除炮塔后格子标记释放 |
| MD-03 | PlaceTower_InvalidCell | 不可放置格子返回失败 |
| MD-04 | PlaceTower_OccupiedCell | 已占据格子返回失败 |
| MD-05 | QueryCell_CorrectState | 查询返回正确状态 |

---

## 4. 测试执行方式

```bash
# EditMode 测试（纯逻辑 + ECS 集成）
Unity Test Runner → EditMode → TowerDefense.Tests

# PlayMode 测试（性能基准）
Unity Test Runner → PlayMode → TowerDefense.Tests

# 命令行执行
unity -runTests -testPlatform EditMode -testFilter TowerDefense
```

---

## 5. 通过标准

- ✅ 所有功能测试用例通过
- ✅ 所有性能指标达标
- ✅ 零 GC Allocation（每帧热路径）
- ✅ 零编译警告
- ✅ Burst 编译无报错

