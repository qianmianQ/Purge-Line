// ============================================================================
// PurgeLine.Resource — LoadPriority.cs
// 加载优先级枚举，数值越小优先级越高
// ============================================================================

namespace PurgeLine.Resource
{
    /// <summary>
    /// 资源加载优先级。数值越小越优先出队。
    /// </summary>
    public enum LoadPriority : byte
    {
        /// <summary>最高优先级：战斗/关键 UI 资源</summary>
        Critical = 0,

        /// <summary>高优先级：即将进入视野的资源</summary>
        High = 50,

        /// <summary>普通优先级：常规加载</summary>
        Normal = 100,

        /// <summary>低优先级：后台预加载、非关键装饰</summary>
        Low = 150,

        /// <summary>最低优先级：空闲时静默加载</summary>
        Background = 200,
    }
}

