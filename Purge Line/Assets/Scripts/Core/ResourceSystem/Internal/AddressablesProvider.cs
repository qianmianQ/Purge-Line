// ============================================================================
// PurgeLine.Resource.Internal — AddressablesProvider.cs
// Addressables 底层适配层，统一封装所有 Addressables API 为 UniTask
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PurgeLine.Resource.Internal
{
    /// <summary>
    /// Addressables API 适配层。
    /// 所有 Addressables 调用集中在此类，便于 mock 测试和统一异常处理。
    /// 使用 AddressableUniTaskBridge 将 AsyncOperationHandle 转换为 UniTask。
    /// </summary>
    internal sealed class AddressablesProvider
    {
        private readonly ILogger _logger;

        public AddressablesProvider(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 按 address 异步加载资源（泛型）
        /// </summary>
        public async UniTask<(T asset, AsyncOperationHandle handle)> LoadAssetAsync<T>(
            string address, CancellationToken ct = default) where T : class
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                T result = await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    // 转为非泛型句柄存储，便于统一管理
                    return (result, (AsyncOperationHandle)handle);
                }

                throw new InvalidOperationException(
                    $"[AddressablesProvider] Load failed for '{address}': Status={handle.Status}");
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
        }

        /// <summary>
        /// 按 AssetReference 异步加载资源
        /// </summary>
        public async UniTask<(T asset, AsyncOperationHandle handle)> LoadAssetAsync<T>(
            AssetReference reference, CancellationToken ct = default) where T : class
        {
            var handle = reference.LoadAssetAsync<T>();
            try
            {
                T result = await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return (result, (AsyncOperationHandle)handle);
                }

                throw new InvalidOperationException(
                    $"[AddressablesProvider] Load failed for AssetReference '{reference.RuntimeKey}': Status={handle.Status}");
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
        }

        /// <summary>
        /// 按 Label 批量加载资源
        /// </summary>
        public async UniTask<(IList<T> assets, AsyncOperationHandle handle)> LoadAssetsByLabelAsync<T>(
            string label, CancellationToken ct = default)
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            try
            {
                IList<T> result = await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return (result, (AsyncOperationHandle)handle);
                }

                throw new InvalidOperationException(
                    $"[AddressablesProvider] Label load failed for '{label}': Status={handle.Status}");
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
        }

        /// <summary>
        /// 异步实例化 GameObject
        /// </summary>
        public async UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            CancellationToken ct = default)
        {
            var handle = Addressables.InstantiateAsync(address, position, rotation, parent);
            try
            {
                GameObject result = await handle.ToUniTask(cancellationToken: ct);
                if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
                {
                    return result;
                }

                throw new InvalidOperationException(
                    $"[AddressablesProvider] Instantiate failed for '{address}': Status={handle.Status}");
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                {
                    if (handle.Result != null)
                        Addressables.ReleaseInstance(handle.Result);
                    else
                        Addressables.Release(handle);
                }
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
        }

        /// <summary>
        /// 释放实例化的 GameObject
        /// </summary>
        public void ReleaseInstance(GameObject go)
        {
            if (go != null)
            {
                Addressables.ReleaseInstance(go);
            }
        }

        /// <summary>
        /// 释放 AsyncOperationHandle
        /// </summary>
        public void ReleaseHandle(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}

