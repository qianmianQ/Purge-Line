using System.Collections;
using UnityEngine;

namespace UnitySystemArchitecture.Core
{
    /// <summary>
    /// 协程运行器接口
    /// 纯C#类无法直接启动协程，需要通过此接口代理
    /// </summary>
    public interface ICoroutineRunner
    {
        /// <summary>
        /// 启动协程
        /// </summary>
        Coroutine StartCoroutine(IEnumerator routine);

        /// <summary>
        /// 停止协程
        /// </summary>
        void StopCoroutine(Coroutine routine);
    }
}
