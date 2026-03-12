namespace PurgeLine.Events
{
    /// <summary>Global 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum GlobalEvent
    {
        None = 0,
        // 在此添加全局事件，例如：
        // GamePaused,
        // GameResumed,
        // ApplicationQuit,
        Max,
    }
}
