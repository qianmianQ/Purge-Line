namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 框架系统基础接口，所有 Unity 系统必须实现
    /// 与 ECS 的 ISystem（Logic 命名空间）区分开
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 系统初始化，在注册时立即调用
        /// </summary>
        void OnInit();

        /// <summary>
        /// 系统销毁，在注销或游戏结束时调用
        /// </summary>
        void OnDispose();
    }
}