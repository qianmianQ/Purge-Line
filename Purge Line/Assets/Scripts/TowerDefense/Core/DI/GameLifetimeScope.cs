using System;
using PurgeLine.Resource;
using TowerDefense.Bridge;
using TowerDefense.ECS.Bridge;
using TowerDefense.ECS.Lifecycle;
using TowerDefense.TowerDefense.Utilities.GameObjectPool;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    public static GameLifetimeScope Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        base.Awake();
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        var framework = transform.parent.GetComponent<GameFramework>();
        if (framework == null)
        {
            framework = FindObjectOfType<GameFramework>();
            if (framework == null)
            {
                throw new InvalidOperationException("GameFramework instance not found in the scene. Please ensure there is a GameObject with GameFramework component.");
            }
        }

        var resourceManager = framework.GetResourceManager();
        builder.RegisterComponent(framework).AsSelf();
        
        builder.RegisterInstance(resourceManager)
            .AsSelf()
            .As<IResourceManager>()
            .As<IStartable>()
            .As<ITickable>()
            .As<IInitializable>()
            .As<IDisposable>();   
        
        builder.RegisterEntryPoint<EcsLifecycleService>()
            .As<IEcsLifecycleService>()
            .As<IEcsWorldAccessor>();
        builder.RegisterEntryPoint<GridBridgeSystem>().As<IGridBridgeSystem>();
        builder.RegisterEntryPoint<CombatBridgeSystem>().As<ICombatBridgeSystem>();
        builder.RegisterEntryPoint<TowerPlacementSystem>();
        builder.RegisterEntryPoint<EcsVisualBridgeSystem>().As<IEcsVisualBridgeSystem>();
        builder.Register<GameObjectPoolManager>(Lifetime.Transient).As<IGameObjectPoolManager>();
    }
}