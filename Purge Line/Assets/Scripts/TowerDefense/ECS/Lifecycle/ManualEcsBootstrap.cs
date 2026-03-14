using Unity.Entities;
using UnityEngine.Scripting;

namespace TowerDefense.ECS.Lifecycle
{
    /// <summary>
    /// 禁用 Entities 默认自动 World 启动，改由 EcsLifecycleService 手动创建。
    /// </summary>
    [Preserve]
    public sealed class ManualEcsBootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            return true;
        }
    }
}

