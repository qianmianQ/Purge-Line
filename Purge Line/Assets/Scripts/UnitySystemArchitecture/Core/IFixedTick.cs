namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 固定频率更新接口，实现此接口的系统会按固定时间间隔调用 OnFixedTick
    /// </summary>
    public interface IFixedTick
    {
        /// <summary>
        /// 固定频率更新，在 SystemManager 的 FixedUpdate 中调用
        /// </summary>
        /// <param name="fixedDeltaTime">固定时间间隔（秒）</param>
        void OnFixedTick(float fixedDeltaTime);
    }
}
