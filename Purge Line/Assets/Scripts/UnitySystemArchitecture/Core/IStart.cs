namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 启动接口，实现此接口的系统会在第一帧 Update 前调用一次 OnStart
    /// </summary>
    public interface IStart
    {
        /// <summary>
        /// 在第一帧 Update 之前调用一次
        /// </summary>
        void OnStart();
    }
}
