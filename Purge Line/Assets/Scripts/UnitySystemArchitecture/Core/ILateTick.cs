namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// Late 更新接口，实现此接口的系统会在所有 ITick 执行完后调用 OnLateTick
    /// </summary>
    public interface ILateTick
    {
        /// <summary>
        /// Late 更新，在 SystemManager 的 LateUpdate 中调用
        /// </summary>
        /// <param name="deltaTime">帧间隔时间（秒）</param>
        void OnLateTick(float deltaTime);
    }
}
