using Base.BaseSystem.EventSystem;
using Microsoft.Extensions.Logging;
using PurgeLine.Events;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.Bridge
{
    /// <summary>
    /// 炮塔放置系统 — MonoBehaviour 层，处理输入和虚影显示
    ///
    /// 职责：
    /// 1. 监听 P 键进入/退出放置模式
    /// 2. 鼠标位置吸附到最近格子
    /// 3. 显示虚影（半透明预览）和放置提示物体
    /// 4. 格子校验：可放置→白色虚影，不可放置→红色虚影+日志
    /// 5. 左键点击确认放置
    ///
    /// 注册到 DependencyManager 管理生命周期。
    /// </summary>
    public class TowerPlacementSystem : IInitializable, IStartable, ITickable, System.IDisposable
    {
        private static ILogger _logger;

        // ── 状态 ──────────────────────────────────────────────
        private bool _isPlacementMode;
        private int2 _currentGridCoord;
        private bool _canPlace;

        // ── 虚影和提示 GameObject ──────────────────────────────
        private GameObject _ghostTower;
        private GameObject _tipObject;
        private SpriteRenderer _ghostRenderer;
        private Camera _mainCamera;

        // ── 颜色配置 ──────────────────────────────────────────
        private static readonly Color ValidColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color InvalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);

        // ── 输入 ──────────────────────────────────────────────
        private Keyboard _keyboard;
        private Mouse _mouse;

        // ── 引用 ──────────────────────────────────────────────
        private readonly IGridBridgeSystem _gridBridge;
        private readonly ICombatBridgeSystem _combatBridge;

        public TowerPlacementSystem(IGridBridgeSystem gridBridge, ICombatBridgeSystem combatBridge)
        {
            _gridBridge = gridBridge;
            _combatBridge = combatBridge;
        }

        // ── IInitializable ────────────────────────────────────

        public void Initialize()
        {
            _logger = GameLogger.Create("TowerPlacementSystem");
            _logger.LogInformation("[TowerPlacementSystem] Initialized");
        }

        public void Start()
        {
            _mainCamera = Camera.main;
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;

            if (_gridBridge == null)
                _logger.LogError("[TowerPlacementSystem] GridBridgeSystem not found!");
            if (_combatBridge == null)
                _logger.LogError("[TowerPlacementSystem] CombatBridgeSystem not found!");

            _logger.LogInformation("[TowerPlacementSystem] Started");
        }

        public void Dispose()
        {
            ExitPlacementMode();
            _logger.LogInformation("[TowerPlacementSystem] Disposed");
        }

        // ── ITickable ─────────────────────────────────────────

        public void Tick()
        {
            if (_keyboard == null || _mouse == null) return;

            // P键切换放置模式
            if (_keyboard.pKey.wasPressedThisFrame)
            {
                if (_isPlacementMode)
                    ExitPlacementMode();
                else
                    EnterPlacementMode();
            }

            // ESC 退出放置模式
            if (_isPlacementMode && _keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitPlacementMode();
            }

            if (!_isPlacementMode) return;

            // 更新虚影位置
            UpdateGhostPosition();

            // 左键点击放置
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                TryPlaceTower();
            }
        }

        // ── 放置模式管理 ─────────────────────────────────────

        private void EnterPlacementMode()
        {
            if (_isPlacementMode) return;
            if (_gridBridge == null || !_gridBridge.IsMapLoaded)
            {
                _logger.LogWarning("[TowerPlacementSystem] Cannot enter placement mode: map not loaded");
                return;
            }

            _isPlacementMode = true;

            // 创建虚影 — 使用简单的 Sprite 作为虚影
            CreateGhostObjects();

            EventManager.Gameplay.Dispatch(new PlacementModeChangedEvent { IsActive = true });
            _logger.LogInformation("[TowerPlacementSystem] Entered placement mode");
        }

        private void ExitPlacementMode()
        {
            if (!_isPlacementMode) return;
            _isPlacementMode = false;

            DestroyGhostObjects();

            EventManager.Gameplay.Dispatch(new PlacementModeChangedEvent { IsActive = false });
            _logger.LogInformation("[TowerPlacementSystem] Exited placement mode");
        }

        // ── 虚影管理 ─────────────────────────────────────────

        private void CreateGhostObjects()
        {
            // 创建虚影炮塔（程序化创建，避免 Resources 路径依赖）
            if (_ghostTower == null)
            {
                _ghostTower = new GameObject("[GhostTower]");
                _ghostRenderer = _ghostTower.AddComponent<SpriteRenderer>();
                _ghostRenderer.color = ValidColor;
                _ghostRenderer.sortingOrder = 100;

                // 创建 1x1 白色方块作为虚影占位
                // 后续可通过 ResourceManager 异步加载实际炮塔 Sprite 替换
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _ghostRenderer.sprite = Sprite.Create(tex,
                    new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            // 创建提示物体（程序化创建）
            // 后续可通过 ResourceManager 异步加载 ui tip.prefab 替换
            if (_tipObject == null)
            {
                _tipObject = new GameObject("[PlacementTip]");
                var tipRenderer = _tipObject.AddComponent<SpriteRenderer>();
                tipRenderer.sortingOrder = 99;
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0.3f, 0.8f, 0.3f, 0.3f));
                tex.Apply();
                tipRenderer.sprite = Sprite.Create(tex,
                    new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
        }

        private void DestroyGhostObjects()
        {
            if (_ghostTower != null)
            {
                Object.Destroy(_ghostTower);
                _ghostTower = null;
                _ghostRenderer = null;
            }

            if (_tipObject != null)
            {
                Object.Destroy(_tipObject);
                _tipObject = null;
            }
        }

        // ── 虚影位置更新 ─────────────────────────────────────

        private void UpdateGhostPosition()
        {
            if (_mainCamera == null || _ghostTower == null) return;

            // 获取鼠标世界坐标
            Vector2 mouseScreenPos = _mouse.position.ReadValue();
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(
                new Vector3(mouseScreenPos.x, mouseScreenPos.y, -_mainCamera.transform.position.z));

            float2 worldPos2D = new float2(mouseWorldPos.x, mouseWorldPos.y);

            // 吸附到最近格子
            _currentGridCoord = _gridBridge.WorldToGrid(worldPos2D);
            float2 snappedPos = _gridBridge.GridToWorld(_currentGridCoord);

            // 更新虚影位置
            _ghostTower.transform.position = new Vector3(snappedPos.x, snappedPos.y, 0f);
            if (_tipObject != null)
            {
                _tipObject.transform.position = new Vector3(snappedPos.x, snappedPos.y, 0f);
            }

            // 校验是否可放置
            _canPlace = _gridBridge.CanPlaceAt(_currentGridCoord);

            // 更新虚影颜色
            if (_ghostRenderer != null)
            {
                _ghostRenderer.color = _canPlace ? ValidColor : InvalidColor;
            }
        }

        // ── 放置执行 ─────────────────────────────────────────

        private void TryPlaceTower()
        {
            if (!_canPlace)
            {
                _logger.LogWarning("[TowerPlacementSystem] Cannot place tower at ({0},{1}) — cell is not placeable or occupied",
                    _currentGridCoord.x, _currentGridCoord.y);
                return;
            }

            // 通过 CombatBridgeSystem 创建炮塔
            var towerEntity = _combatBridge.CreateTower(_currentGridCoord);
            if (towerEntity == Entity.Null)
            {
                _logger.LogError("[TowerPlacementSystem] Failed to create tower at ({0},{1})",
                    _currentGridCoord.x, _currentGridCoord.y);
                return;
            }

            _logger.LogInformation("[TowerPlacementSystem] Tower placed at ({0},{1})",
                _currentGridCoord.x, _currentGridCoord.y);

            // 放置成功后退出放置模式
            ExitPlacementMode();
        }
    }
}

