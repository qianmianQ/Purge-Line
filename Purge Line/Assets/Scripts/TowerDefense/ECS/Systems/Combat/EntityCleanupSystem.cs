using TowerDefense.ECS;
using TowerDefense.ECS.Bridge;
using Unity.Collections;
using Unity.Entities;
using UnityDependencyInjection;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 实体清理系统 — 托管清理版本
    ///
    /// 职责：
    /// 1. 查询所有带 DestroyTag 的实体
    /// 2. 对带 VisualLink 的实体，回收关联的 GameObject
    /// 3. 批量销毁实体
    ///
    /// 说明：
    /// - 本系统运行在主线程，允许访问托管对象池桥接层
    /// - 通过 EntityCommandBuffer 批量销毁实体
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct EntityCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DestroyTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 查询所有带 DestroyTag 的实体
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<DestroyTag>>()
                    .WithEntityAccess())
            {
                // 如果实体有 VisualLink 组件，先回收视图
                if (state.EntityManager.HasComponent<VisualLink>(entity))
                {
                    var link = state.EntityManager.GetComponentData<VisualLink>(entity);
                    DependencyManager.Instance.Get<EcsVisualBridgeSystem>().ReturnGameObjectInPool(link.GameObjectRef.Value, link.PrefabAddress.ToString());
                }

                // 销毁实体
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
