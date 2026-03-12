using System;
using UnityEngine;
using PurgeLine.Events;

namespace Base.BaseSystem.EventSystem
{
    /// <summary>
    /// 全局事件系统入口。每个域持有独立的 EventBus，实现真正的域隔离。
    ///
    /// 用法：
    ///   EventSystem.Gameplay.Dispatch(GamePlayEvent.WaveCompleted);
    ///   EventSystem.Gameplay.Dispatch(new TowerPlacedEvent(...));
    ///   EventSystem.Gameplay.AddListener<TowerPlacedEvent>(OnTowerPlaced);
    ///
    /// 生命周期：由 GameFramework.OnDestroy() 调用 EventSystem.Dispose()，勿手动调用。
    ///
    /// ⚠️ Unity Editor 注意：三个域字段（UI/Gameplay/Global）为 static 属性，
    /// 只能通过 Init 方法初始化，不能修改。
    /// 需要 Unity "Reload Domain" 选项保持启用（Project Settings → Editor → Enter Play Mode Settings），
    /// 否则跨 Play Mode 的域状态无法重置。IsDisposed 通过 RuntimeInitializeOnLoadMethod 在每次
    /// Play Mode 进入时自动重置为 false。
    /// </summary>
    public static class GameEventSystem
    {
        private static EventDomain _ui;
        private static EventDomain _gameplay;
        private static EventDomain _global;

        public static EventDomain UI
        {
            get
            {
                CheckReady();
                return _ui;
            }
        }

        public static EventDomain Gameplay
        {
            get
            {
                CheckReady();
                return _gameplay;
            }
        }

        public static EventDomain Global
        {
            get
            {
                CheckReady();
                return _global;
            }
        }

        /// <summary>事件系统是否已销毁</summary>
        public static bool IsDisposed { get; private set; }
        private static bool _initialized;

        /// <summary>
        /// 初始化事件系统，传入具体枚举类型。
        /// 只能调用一次。
        /// </summary>
        public static void Init(Type uiEnumType, Type gameplayEnumType, Type globalEnumType)
        {
            if (_initialized)
            {
                Debug.LogError("EventSystem.Init can only be called once.");
                return;
            }
            
            _ui = new EventDomain(new EventBus(), uiEnumType);
            _gameplay = new EventDomain(new EventBus(), gameplayEnumType);
            _global = new EventDomain(new EventBus(), globalEnumType);
            _initialized = true;
        }

        /// <summary>
        /// 在每次 Play Mode 进入时重置 IsDisposed 标志。
        /// readonly 域字段由 Reload Domain 负责重新初始化（见类注释）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            IsDisposed = false;
            _initialized = false;
            _ui = null;
            _gameplay = null;
            _global = null;
        }

        /// <summary>
        /// 销毁所有域。由 GameFramework.OnDestroy() 调用，勿手动调用。
        /// </summary>
        public static void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _ui?.Dispose();
            _gameplay?.Dispose();
            _global?.Dispose();
        }

        private static void CheckReady()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException("GameEventSystem");
            }
            if (!_initialized)
            {
                throw new InvalidOperationException("GameEventSystem is not initialized. Call GameEventSystem.Init() before using.");
            }
        }
    }
}
