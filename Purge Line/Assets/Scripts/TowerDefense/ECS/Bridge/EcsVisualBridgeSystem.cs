// ============================================================================
// TowerDefense.ECS.Bridge — EcsVisualBridge.cs
// ECS ↔ GameObject 可视化桥接器
// 由 DependencyManager 管理生命周期
// ============================================================================

using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TowerDefense.TowerDefense.Utilities.GameObjectPool;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.ECS.Bridge
{
    /// <summary>
    /// ECS ↔ GameObject 可视化桥接器
    ///
    /// ── 职责 ──────────────────────────────────────────────
    /// 1. 检测带有 VisualRequest 的 ECS 实体 → 实例化对应 Prefab
    /// 2. 每帧同步 ECS LocalTransform → GameObject Transform
    /// 3. 检测带有 DestroyTag / 已被 EntityManager 销毁的实体 → 回收 GameObject
    ///
    /// ── 使用方式 ──────────────────────────────────────────
    /// 无需在场景中手动挂载，由 DependencyManager 自动管理生命周期。
    /// Prefab 配置通过 EcsVisualBridgeConfig ScriptableObject 指定。
    ///
    /// ── 性能说明 ──────────────────────────────────────────
    /// 使用 EntityQuery + NativeArray 批量同步，避免逐实体 GetComponent。
    /// GameObject 层面使用 ResourceManager 的对象池减少 Instantiate/Destroy 开销。
    /// </summary>
    public class EcsVisualBridgeSystem : IEcsVisualBridgeSystem, IInitializable, System.IDisposable
    {
        private ILogger _logger;

        private IGameObjectPoolManager _entityPoolManager;
        
        private readonly IObjectResolver _container;
        
        public EcsVisualBridgeSystem(IObjectResolver container)
        {
            _container = container;
        }

        public void Initialize()
        {
            _logger = GameLogger.Create<EcsVisualBridgeSystem>();
            _logger.LogInformation("[EcsVisualBridge] Initialized");

            _entityPoolManager = _container.Resolve<IGameObjectPoolManager>();
        }

        public void InitEntitiesPools(string[] addresses)
        {
            foreach (var address in addresses)
            {
                // ECS 已维护 PrefabAddress，回收时会显式传 address，关闭内部 instance->address 追踪以减少字典写入开销。
                _entityPoolManager.CreatePoolAsync(address, trackInstanceAddress: false);
                // if (address == "Assets/Prefabs/Gameplay/Enemies/Enemy Entity.prefab")
                // {
                //     var task = _entityPoolManager.PreloadAsync(address, 10000, 20);
                //     task.GetAwaiter().OnCompleted(() =>
                //     {
                //         _logger.LogInformation(
                //             $"[EcsVisualBridge] Preloaded pool for {address} with 10000 instances");
                //     });
                // }
                //
                // if (address == "Assets/Prefabs/Gameplay/Bullet.prefab")
                // {
                //     var task = _entityPoolManager.PreloadAsync(address, 5000, 10);
                //     task.GetAwaiter().OnCompleted(() =>
                //     {
                //         _logger.LogInformation(
                //             $"[EcsVisualBridge] Preloaded pool for {address} with 10000 instances");
                //     });
                // }
                    
            }
        }

        public GameObject GetGameObjectInPoolSync(string address)
        {
            return _entityPoolManager.Get(address);
        }

        public UniTask<GameObject> GetGameObjectInPoolASync(string address)
        {
            return _entityPoolManager.GetAsync(address);
        }

        public void ReturnGameObjectInPool(GameObject obj, string address = null)
        {
            if (obj == null)
            {
                _logger.LogError("[EcsVisualBridge] Attempted to return null GameObject to pool");
                return;
            }
            _entityPoolManager.Return(obj, address);
            obj.SetActive(false);
        }

        public void Dispose()
        {
            _entityPoolManager.Clear();
        }
    }
}
