// using Microsoft.Extensions.Logging;
// using TowerDefense.ECS.Bridge;
// using Unity.Entities;
// using UnityDependencyInjection;
// using ILogger = Microsoft.Extensions.Logging.ILogger;
//
// namespace TowerDefense.ECS
// {
//     /// <summary>
//     /// 视图清理系统 — 托管 System，处理 GameObject 回收
//     ///
//     /// 职责：
//     /// 1. 查询所有带 VisualCleanupRequest 的实体
//     /// 2. 通过 VisualLink 获取 GameObject 引用
//     /// 3. 调用 EcsVisualBridgeSystem 回收 GameObject 到对象池
//     /// 4. 移除 VisualLink 和 VisualCleanupRequest 组件
//     ///
//     /// 架构设计：
//     /// - 本系统继承 SystemBase，在主线程运行，可安全访问托管对象
//     /// - 与 Burst 编译的 EntityCleanupSystem 配合工作
//     /// - VisualCleanupRequest 作为跨系统通信的标记组件
//     ///
//     /// 执行顺序：
//     /// - 在 SimulationSystemGroup 中，于 EntityCleanupSystem 之后运行
//     /// - 确保 EntityCleanupSystem 已经标记了需要清理的实体
//     /// </summary>
//     [UpdateInGroup(typeof(SimulationSystemGroup))]
//     [UpdateAfter(typeof(EntityCleanupSystem))]
//     public partial class VisualCleanupSystem : SystemBase
//     {
//         private ILogger _logger;
//         private EcsVisualBridgeSystem _visualBridge;
//
//         protected override void OnCreate()
//         {
//             _logger = GameLogger.Create<VisualCleanupSystem>();
//
//             // 获取 EcsVisualBridgeSystem 引用
//             // 注意：使用 try-catch 防止初始化时依赖不可用
//             try
//             {
//                 _visualBridge = DependencyManager.Instance?.Get<EcsVisualBridgeSystem>();
//             }
//             catch (System.Exception ex)
//             {
//                 _logger.LogWarning($"[VisualCleanupSystem] Failed to get EcsVisualBridgeSystem: {ex.Message}");
//             }
//
//             // 要求有 VisualCleanupRequest 组件才执行
//             RequireForUpdate<VisualCleanupRequest>();
//
//             _logger.LogInformation("[VisualCleanupSystem] Created");
//         }
//
//         protected override void OnUpdate()
//         {
//             // 如果 bridge 不可用，跳过处理
//             if (_visualBridge == null)
//             {
//                 // 尝试重新获取
//                 try
//                 {
//                     _visualBridge = DependencyManager.Instance?.Get<EcsVisualBridgeSystem>();
//                 }
//                 catch
//                 {
//                     return;
//                 }
//
//                 if (_visualBridge == null) return;
//             }
//
//             // 将成员字段复制到局部变量，避免 Entities.ForEach Lambda 捕获 this
//             var bridgeLocal = _visualBridge;
//             var loggerLocal = _logger;
//             var entityManager = EntityManager;
//
//             var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
//
//             // 查询所有带 VisualCleanupRequest 的实体
//             // 注意：使用局部变量而不是成员字段
//             // 使用 .WithoutBurst().Run() 允许 Lambda 捕获引用类型（如 EcsVisualBridgeSystem）
//             Entities
//                 .WithAll<VisualCleanupRequest>()
//                 .ForEach((Entity entity, in VisualLink visualLink) =>
//                 {
//                     // 获取 GameObject 引用
//                     var go = visualLink.GameObjectRef.Value;
//
//                     if (go != null)
//                     {
//                         // 回收 GameObject 到对象池（使用局部变量）
//                         bridgeLocal.ReturnGameObjectInPool(go);
//
//                         if (loggerLocal?.IsEnabled(LogLevel.Debug) == true)
//                         {
//                             loggerLocal.LogDebug($"[VisualCleanupSystem] Returned GameObject to pool: {go.name}");
//                         }
//                     }
//                     else if (loggerLocal?.IsEnabled(LogLevel.Warning) == true)
//                     {
//                         loggerLocal.LogWarning($"[VisualCleanupSystem] VisualLink has null GameObject for entity {entity}");
//                     }
//
//                     // 移除组件
//                     ecb.RemoveComponent<VisualLink>(entity);
//                     ecb.RemoveComponent<VisualCleanupRequest>(entity);
//
//                 }).WithoutBurst().Run(); // .WithoutBurst().Run() 允许捕获引用类型
//
//             ecb.Playback(entityManager);
//             ecb.Dispose();
//         }
//
//         protected override void OnDestroy()
//         {
//             _logger?.LogInformation("[VisualCleanupSystem] Destroyed");
//         }
//     }
// }
