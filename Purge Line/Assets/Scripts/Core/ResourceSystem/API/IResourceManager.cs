// ============================================================================
// PurgeLine.Resource — IResourceManager.cs
// 资源管理器公开接口。所有外部调用通过此接口，便于测试与替换。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PurgeLine.Resource
{
    /// <summary>
    /// 资源管理器公开接口。外部应仅依赖此接口。
    /// </summary>
    public interface IResourceManager
    {
        // ── 异步加载（核心 API）────────────────────────────────────

        /// <summary>按 Address 异步加载资源</summary>
        UniTask<ResourceHandle<T>> LoadAsync<T>(
            string address,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class;

        /// <summary>按 AssetReference 异步加载资源</summary>
        UniTask<ResourceHandle<T>> LoadAsync<T>(
            AssetReference assetReference,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class;

        /// <summary>按 Label 批量异步加载资源</summary>
        UniTask<IReadOnlyList<ResourceHandle<T>>> LoadByLabelAsync<T>(
            string label,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default) where T : class;

        // ── 同步加载（仅缓存命中时有效）──────────────────────────

        /// <summary>
        /// 尝试从缓存同步获取资源。未缓存时返回 Invalid Handle。
        /// 不会触发新的异步加载。
        /// </summary>
        ResourceHandle<T> TryGetCached<T>(string address) where T : class;

        // ── 释放 ───────────────────────────────────────────────

        /// <summary>释放单个资源句柄（引用计数 -1）</summary>
        void Release<T>(ResourceHandle<T> handle) where T : class;

        /// <summary>按 Label 批量释放</summary>
        void ReleaseByLabel(string label);

        /// <summary>按场景分组释放</summary>
        void ReleaseByScene(string sceneName);

        /// <summary>全量释放所有资源（慎用）</summary>
        void ReleaseAll();

        // ── 实例化与对象池 ─────────────────────────────────────

        /// <summary>异步实例化 GameObject（优先从对象池取）</summary>
        UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            LoadPriority priority = LoadPriority.Normal,
            CancellationToken ct = default);

        /// <summary>归还 GameObject 到对象池</summary>
        void ReturnToPool(GameObject instance);

        // ── 预加载 ────────────────────────────────────────────

        /// <summary>
        /// 批量预加载资源，支持进度回调。
        /// </summary>
        UniTask PreloadAsync(
            IReadOnlyList<string> addresses,
            LoadPriority priority = LoadPriority.Low,
            IProgress<PreloadProgressEvent> progress = null,
            CancellationToken ct = default);

        // ── 场景分组标记 ──────────────────────────────────────

        /// <summary>将资源标记到指定场景分组，用于按场景批量释放</summary>
        void TagScene(string address, string sceneName);

        // ── 诊断查询 ──────────────────────────────────────────

        /// <summary>获取指定资源的当前引用计数（诊断用）</summary>
        int GetRefCount(string address);

        /// <summary>获取当前缓存的资源数量</summary>
        int CachedCount { get; }

        /// <summary>获取 LRU 缓存中的资源数量</summary>
        int LruCount { get; }

        /// <summary>获取加载队列中待处理的请求数</summary>
        int PendingLoadCount { get; }
    }
}

