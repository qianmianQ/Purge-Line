using UnityEngine;

namespace UnityDependencyInjection
{
    /// <summary>
    /// 游戏启动引导器
    /// 负责在游戏开始时注册所有系统
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("设置")]
        [SerializeField] private bool _autoRegisterInAwake = true;
        [SerializeField] private bool _logRegistrationOrder = true;

        private void Awake()
        {
            // 确保SystemManager已创建
            var manager = DependencyManager.Instance;

            if (_autoRegisterInAwake)
            {
                RegisterAllSystems();
            }

            Debug.Log("[GameBootstrapper] 游戏启动完成");
        }

        /// <summary>
        /// 注册所有系统 - 在此添加你的系统
        /// 注册顺序即Tick执行顺序
        /// </summary>
        protected virtual void RegisterAllSystems()
        {
            var manager = DependencyManager.Instance;

            if (_logRegistrationOrder)
            {
                Debug.Log("========== 开始注册系统 ==========");
            }

            // ==================== 系统注册区域 ====================
            // 按照依赖关系排列顺序：被依赖的先注册

            // 示例：基础系统先注册
            // manager.Register(new ResourceSystem());
            // manager.Register(new AudioSystem());
            // manager.Register(new NetworkSystem());
            // manager.Register(new PlayerSystem());
            // manager.Register(new UISystem());

            if (_logRegistrationOrder)
            {
                Debug.Log("========== 系统注册完成 ==========");
            }
        }

        /// <summary>
        /// 动态注册系统（运行时）
        /// </summary>
        public void RegisterSystem<T>(T system) where T : IInitializable
        {
            DependencyManager.Instance.Register(system);
        }

        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void Restart()
        {
            Debug.Log("[GameBootstrapper] 重新开始游戏...");
            DependencyManager.Instance.DisposeAll();
            RegisterAllSystems();
        }
    }
}
