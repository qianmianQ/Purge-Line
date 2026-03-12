namespace PurgeLine.Events
{
    /// <summary>Gameplay 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum GamePlayEvent
    {
        None = 0,
        // 在此添加 Gameplay 事件，例如：
        // WaveStarted,
        // WaveCompleted,
        // TowerPlaced,
        Max,
    }
}
