namespace PurgeLine.Events
{
    /// <summary>UI 域无参事件枚举。值 0 (None) 为保留值，不可用于 Dispatch/AddListener。</summary>
    public enum UIEvent
    {
        None = 0,
        // 在此添加 UI 事件，例如：
        // OpenPanel,
        // ClosePanel,
        Max,
    }
}
