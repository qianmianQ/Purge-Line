namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 帧更新接口，实现此接口的系统会每帧调用 OnTick
    /// </summary>
    public interface ITick
    {
        /// <summary>
        /// 每帧更新，在 SystemManager 的 Update 中调用
        /// </summary>
        /// <param name="deltaTime">帧间隔时间（秒）</param>
        void OnTick(float deltaTime);
    }
}
