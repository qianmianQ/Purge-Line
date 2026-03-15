# ECS Lifecycle 使用文档

## 运行流程
1. 玩家在 UI 中选择关卡。
2. 调用 `GameFramework.StartGameSession(levelId)`。
3. 框架内部自动：
   - 启动 `IEcsLifecycleService` 创建手动 World。
   - 自动注册所有 ECS 系统。
   - 通过 `IGridBridgeSystem.LoadLevel` 发送 `GridSpawnRequest`。
4. 结束游戏时调用 `GameFramework.StopGameSession()`。

## 代码调用示例

```csharp
public class LevelSelectPresenter
{
    private readonly GameFramework _framework;

    public LevelSelectPresenter(GameFramework framework)
    {
        _framework = framework;
    }

    public void OnConfirmLevel(string levelId)
    {
        bool started = _framework.StartGameSession(levelId);
        if (!started)
        {
            // TODO: UI 提示
        }
    }

    public void OnExitBattle()
    {
        _framework.StopGameSession();
    }
}
```

## 编辑器调试
- 打开 `PurgeLine/ECS Lifecycle Monitor`
- 支持：
  - Start World
  - Pause / Resume
  - Stop World
  - Start Session + Load Level
  - Capture Snapshot

## 运行时可观测指标
- 当前状态（Running/Paused/Stopped）
- 注册系统数
- 实体总数（定频采样）
- 最近帧分组耗时（Init/Sim/Pres）
- 平均帧耗时
- 快照历史（含 note、内存、实体数）

## 常见问题
- **StartWorld 成功但地图不出现**：检查 `levelId` 是否有效，查看 `GridBridgeSystem` 日志。
- **Bridge 报 World 不可用**：确认是否通过 `StartGameSession` 启动，避免直接调用桥接 API。
- **重复启动**：`StartWorld` 已幂等处理，重复调用会被忽略或返回现状。


