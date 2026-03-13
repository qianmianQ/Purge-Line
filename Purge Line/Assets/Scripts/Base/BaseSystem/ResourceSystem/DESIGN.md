# Addressables Resource Management System — 设计文档

## 1. 架构总览

```
┌──────────────────────────────────────────────────────────────────┐
│                     ResourceManager (Façade)                     │
│  IInitializable / ITickable — 注册到 DependencyManager           │
│  公开 API: LoadAsync / Release / Instantiate / Preload / Pool    │
├──────────────┬──────────────┬────────────┬───────────────────────┤
│ LoadQueue    │ RefTracker   │ ResCache   │ ObjectPoolManager     │
│ 优先级调度   │ 引用计数     │ 多级缓存   │ GO 复用池             │
├──────────────┴──────────────┴────────────┴───────────────────────┤
│                  AddressablesProvider (底层适配)                   │
│        封装 Addressables API → UniTask<T>                        │
├──────────────────────────────────────────────────────────────────┤
│  RetryPolicy │ TraceContext │ MemoryGuard │ ResourceMetrics      │
│  重试/降级   │ 追踪上下文   │ 内存水位    │ 监控埋点             │
├──────────────────────────────────────────────────────────────────┤
│  Extension Interfaces (P3 预留)                                   │
│  IResourceDecryptor / IVariantSelector / IHotUpdateCallback /    │
│  IEcsResourceBridge                                              │
└──────────────────────────────────────────────────────────────────┘
```

## 2. 模块划分

| 模块 | 目录 | 职责 |
|------|------|------|
| **API** | `API/` | 公开接口、数据结构、枚举、配置 |
| **Internal** | `Internal/` | 核心实现：加载器、引用计数、缓存、队列 |
| **ObjectPool** | `ObjectPool/` | 通用 GameObject 对象池 |
| **Diagnostics** | `Diagnostics/` | 追踪、指标、内存守卫 |
| **Extensions** | `Extensions/` | P3 预留扩展接口 |
| **Editor** | `Editor/` | 监控窗口、压力测试 |

## 3. 核心类设计

### 3.1 ResourceManager (Façade)
- 实现 `IInitializable`, `ITickable`, `IStartable`
- 构造接收 `ResourceManagerConfig`
- 在 `OnTick` 中驱动 LoadQueue、MemoryGuard、ObjectPool 的超时回收
- 所有公开方法通过 `IResourceManager` 接口暴露

### 3.2 ResourceHandle<T>
- readonly struct，持有 HandleId (uint) + Address (string) + Asset (T)
- 外部只通过 Handle 引用资源，Release 时传入 Handle

### 3.3 ReferenceTracker
- `Dictionary<string, RefEntry>` 维护每个 address 的引用计数
- Retain(address) / Release(address) 原子操作
- 计数归零时回调 OnZeroRef → 触发 ResourceCache 移入 LRU 或释放

### 3.4 ResourceCache
- 热缓存：`Dictionary<string, CacheEntry>` — 正在使用的资源
- LRU 缓存：`LinkedList<LruEntry>` + `Dictionary<string, LinkedListNode>` — 已释放但保留
- 容量配置：最大 LRU 条目数、总内存上限
- Evict 策略：LRU 尾部优先淘汰

### 3.5 LoadQueue
- 基于 `List` 的最小堆实现优先级队列
- `LoadRequest` 包含: Address, Priority, UniTaskCompletionSource, CancellationToken, TraceId
- OnTick 每帧出队 N 个（并发控制），交给 AddressablesProvider 异步执行

### 3.6 AddressablesProvider
- 封装 `Addressables.LoadAssetAsync<T>` → `UniTask<T>`
- 封装 `Addressables.InstantiateAsync` → `UniTask<GameObject>`
- 封装 Label 批量加载 `Addressables.LoadAssetsAsync<T>`
- 封装 `AssetReference.LoadAssetAsync<T>`
- 所有异步统一返回 UniTask，支持 CancellationToken

### 3.7 ObjectPoolManager
- `Dictionary<string, GameObjectPool>` 按 prefab address 分池
- `GameObjectPool`: `Stack<GameObject>` + 容量限制 + 超时自动回收
- Rent → 从池取或 InstantiateAsync → Return → 回池

### 3.8 RetryPolicy
- 可配置: 最大重试次数、基础延迟、退避系数、降级资源 address
- 加载失败自动重试，重试耗尽触发降级 fallback

### 3.9 MemoryGuard
- 配置内存水位线阈值 (MB)
- OnTick 周期性检查 `Profiler.GetTotalAllocatedMemoryLong()`
- 超阈值时通知 ResourceCache 执行 LRU 淘汰
- 触发 EventManager.Global 事件通知

### 3.10 TraceContext
- 每次加载请求生成唯一 TraceId (ulong 自增)
- 关联到所有日志输出，方便线上问题定位

## 4. 关键流程

### 4.1 LoadAsync 流程
```
用户调用 LoadAsync<T>(address, priority)
  → TraceContext 生成 TraceId
  → ReferenceTracker.Retain(address) 引用+1
  → if ResourceCache.TryGetHot(address) → 命中热缓存，直接返回
  → if ResourceCache.TryPromoteLru(address) → 命中 LRU，提升到热缓存
  → LoadQueue.Enqueue(request)  — 优先级入队
  → LoadQueue.OnTick 出队 → RetryPolicy 包装 AddressablesProvider.LoadAsync
  → 成功 → ResourceCache.AddHot(address, asset) → 返回 ResourceHandle<T>
  → 失败 → RetryPolicy 重试/降级 → 返回降级资源或抛异常
```

### 4.2 Release 流程
```
用户调用 Release(handle)
  → ReferenceTracker.Release(address) 引用-1
  → if 引用 > 0 → 返回（仍有其他持有者）
  → if 引用 == 0 → ResourceCache.MoveToLru(address)
  → LRU 满 → 淘汰最旧条目 → AddressablesProvider.ReleaseAsset(handle)
```

### 4.3 Instantiate + Pool 流程
```
用户调用 InstantiateAsync(address, parent)
  → ObjectPoolManager.TryRent(address) → 命中池 → 激活并返回
  → 未命中 → LoadAsync<GameObject>(address) → Addressables.InstantiateAsync
  → 返回 GameObject

用户调用 ReturnToPool(go)
  → ObjectPoolManager.Return(address, go) → 失活并入池
  → if 池满 → Destroy(go) + Release ref
```

## 5. 数据结构

```csharp
// 引用计数条目
struct RefEntry { public int Count; public float LastAccessTime; }

// 缓存条目
struct CacheEntry { public string Address; public object Asset; public AsyncOperationHandle Handle; public long MemorySize; }

// LRU 条目
struct LruEntry { public string Address; public CacheEntry Cache; public float EvictTime; }

// 加载请求
struct LoadRequest { public string Address; public Type AssetType; public LoadPriority Priority; public ulong TraceId; public UniTaskCompletionSource<object> Tcs; public CancellationToken Ct; }
```

## 6. 性能优化方案

1. **零 GC 热路径**: 所有热路径使用 struct、stackalloc、预分配集合，避免闭包和装箱
2. **字符串 intern**: address 字符串使用 `string.Intern` 避免重复分配
3. **UniTask 零分配**: 利用 UniTask 的 pooling 机制避免 Task 分配
4. **批量操作**: Label 加载使用 `LoadAssetsAsync` 一次性加载
5. **帧预算**: LoadQueue 每帧限制出队数量，避免加载高峰卡顿
6. **对象池 Stack**: 使用 Stack<T> 避免 Queue 的 resize

## 7. 风险点与应对

| 风险 | 影响 | 应对 |
|------|------|------|
| Addressables 无真正同步 API | WaitForCompletion 阻塞主线程 | 同步加载仅从缓存取，未缓存返回 null |
| IL2CPP 泛型代码裁剪 | 运行时 MissingMethodException | 使用 `[Preserve]` 标记关键泛型路径 |
| 引用计数不平衡 | 内存泄漏 | Editor 下追踪每次 Retain/Release 调用栈 |
| AsyncOperationHandle 生命周期 | 重复释放崩溃 | CacheEntry 标记 IsReleased 防重复 |
| 多线程竞态 | 字典并发修改 | 所有操作限定主线程，用断言检查 |

## 8. 文件清单

```
Core/ResourceSystem/
├── Core.ResourceSystem.asmdef
├── API/
│   ├── IResourceManager.cs         — 公开接口
│   ├── ResourceHandle.cs           — 资源句柄 (readonly struct)
│   ├── ResourceManagerConfig.cs    — 配置类
│   ├── LoadPriority.cs             — 加载优先级枚举
│   └── ResourceEvents.cs           — 资源相关事件定义
├── Internal/
│   ├── ResourceManager.cs          — 门面实现 (IInitializable)
│   ├── AddressablesProvider.cs     — Addressables 适配层
│   ├── ReferenceTracker.cs         — 引用计数器
│   ├── ResourceCache.cs            — 多级缓存
│   └── LoadQueue.cs                — 优先级加载队列
├── ObjectPool/
│   ├── GameObjectPool.cs           — 单 Prefab 对象池
│   └── ObjectPoolManager.cs        — 对象池管理器
├── Diagnostics/
│   ├── TraceContext.cs             — 追踪上下文
│   ├── MemoryGuard.cs             — 内存水位守卫
│   └── ResourceMetrics.cs         — 监控指标收集
├── Extensions/
│   ├── IResourceDecryptor.cs       — 资源解密接口 (P3)
│   ├── IVariantSelector.cs         — 资源变体选择器 (P3)
│   ├── IHotUpdateCallback.cs       — 热更回调接口 (P3)
│   └── IEcsResourceBridge.cs       — ECS 兼容接口 (P3)
└── Editor/
    ├── Core.ResourceSystem.Editor.asmdef
    ├── ResourceMonitorWindow.cs    — 资源监控 EditorWindow
    └── StressTestTool.cs           — 压力测试工具
```

