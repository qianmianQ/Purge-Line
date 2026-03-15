# 实体数据系统使用文档（重构后）

## 一、运行时注入（VContainer）
`GameLifetimeScope` 已注册：
- `EntityDataService` -> `IEntityDataService`（Singleton）

调用示例：

```csharp
using Cysharp.Threading.Tasks;
using TowerDefense.Data.EntityData;

public sealed class TurretDetailPresenter
{
    private readonly IEntityDataService _entityDataService;

    public TurretDetailPresenter(IEntityDataService entityDataService)
    {
        _entityDataService = entityDataService;
        _entityDataService.EntityDataChanged += OnEntityDataChanged;
    }

    public async UniTask ShowAsync(TurretId turretId)
    {
        TurretConfigPackage package = await _entityDataService.GetTurretAsync(turretId);
        // package.Ui.DisplayName / package.Base.AttackRange ...
    }

    private void OnEntityDataChanged(EntityDataChangeEvent evt)
    {
        // 按 evt.EntityType + evt.LocalId 增量刷新
    }
}
```

## 二、ID 与枚举规则
- `EntityType`：`TURRET/ENEMY/PROJECTILE/Max`
- 分类枚举：`TurretId`、`EnemyId`、`ProjectileId`
- 每个分类枚举都包含：
  - `None = 0`
  - `...业务项...`
  - `Max = 数量`

## 三、中枢窗口（Entity Data Hub）
1. 打开：`PurgeLine/Entity Data Hub`
2. 在顶部输入“新建名称”后，点击某分类“新增配置包”
3. 系统自动：
   - 生成 `Token/EnumName`（来自名称）
   - 生成 address 后缀（来自名称 slug）
   - 生成 `.bytes`
   - 更新索引与分类枚举代码
4. 删除时会自动执行引用检查（若被引用则阻止）
5. 支持：
   - 全部展开/折叠
   - 分类展开/折叠
   - 单项校验
   - 全量校验（含重复 address）
   - 刷新重读

## 四、单体编辑器（按类型）
- 炮塔：`TurretConfigEditorAsset`
- 敌人：`EnemyConfigEditorAsset`
- 子弹：`ProjectileConfigEditorAsset`

功能：
- 编辑各类型独立 `BaseData` / `UIData`
- 拖拽 Sprite/AudioClip 自动写 Addressables address
- `新建实体行为蓝图`：创建后自动打开并加载该蓝图
- `编辑实体蓝图`：按 GUID 直接打开蓝图编辑器
- `保存/加载`：对 `.bytes` 进行 MemoryPack 序列化/反序列化

## 五、FAQ
### Q1: 为什么不再有一个全局 `EntityId`？
A: 业务已改为按类型枚举，避免不同实体类别混用，提高类型安全与可维护性。

### Q2: 删除配置为什么会被阻止？
A: 该地址仍被其他包体引用，系统会给出引用明细，需先解除引用再删。

### Q3: 蓝图新建后为何能直接编辑？
A: 新建流程会直接调用 `EntityBlueprintEditorWindow.OpenAndLoad(path)` 自动打开目标蓝图。

