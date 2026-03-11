namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 可暂停系统接口，实现此接口的系统支持单独暂停
    /// </summary>
    public interface IPausable
    {
        /// <summary>
        /// 暂停状态
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// 暂停时调用
        /// </summary>
        void OnPause();

        /// <summary>
        /// 恢复时调用
        /// </summary>
        void OnResume();
    }
}
