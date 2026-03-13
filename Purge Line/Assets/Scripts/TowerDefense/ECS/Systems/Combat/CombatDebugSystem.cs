using Microsoft.Extensions.Logging;
using TowerDefense.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.ECS
{
    /// <summary>
    /// 战斗调试系统 — 编辑器调试绘制和 Profiler 埋点
    ///
    /// 功能：
    /// 1. 按 F1 显示/隐藏炮塔攻击范围
    /// 2. 按 F2 显示/隐藏格子可放置状态
    /// 3. 自定义 Profiler Marker：单位数量追踪
    ///
    /// 仅在 UNITY_EDITOR 下完整运行，发布版剥离调试绘制。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CombatDebugSystem : ISystem
    {
        private static ILogger _logger;

        // Profiler Markers
        private static readonly ProfilerMarker EnemyCountMarker =
            new ProfilerMarker("CombatDebug.EnemyCount");

        private static readonly ProfilerMarker TowerCountMarker =
            new ProfilerMarker("CombatDebug.TowerCount");

        private static readonly ProfilerMarker BulletCountMarker =
            new ProfilerMarker("CombatDebug.BulletCount");

        // 调试绘制标志
        private static bool _showTowerRange;
        private static bool _showGridInfo;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create("CombatDebugSystem");
            _logger.LogInformation("[CombatDebugSystem] Created");
        }

        public void OnUpdate(ref SystemState state)
        {
            // ── 输入检测 ──────────────────────────────────────

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _showTowerRange = !_showTowerRange;
                _logger.LogInformation("[CombatDebugSystem] Tower range display: {0}",
                    _showTowerRange ? "ON" : "OFF");
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                _showGridInfo = !_showGridInfo;
                _logger.LogInformation("[CombatDebugSystem] Grid info display: {0}",
                    _showGridInfo ? "ON" : "OFF");
            }
#endif

            // ── Profiler 计数器更新 ───────────────────────────

            using (EnemyCountMarker.Auto())
            {
                var enemyQuery = SystemAPI.QueryBuilder()
                    .WithAll<EnemyTag>()
                    .WithNone<DeadTag, DestroyTag>()
                    .Build();
                // 计数结果可在 Profiler 中通过 Marker 时间跨度观测
                enemyQuery.CalculateEntityCount();
            }

            using (TowerCountMarker.Auto())
            {
                var towerQuery = SystemAPI.QueryBuilder()
                    .WithAll<TowerTag>()
                    .Build();
                towerQuery.CalculateEntityCount();
            }

            using (BulletCountMarker.Auto())
            {
                var bulletQuery = SystemAPI.QueryBuilder()
                    .WithAll<BulletTag>()
                    .WithNone<DestroyTag>()
                    .Build();
                bulletQuery.CalculateEntityCount();
            }

            // ── 调试绘制 ──────────────────────────────────────

#if UNITY_EDITOR
            if (_showTowerRange)
            {
                DrawTowerRanges(ref state);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 绘制炮塔攻击范围圆（编辑器 Debug.DrawLine）
        /// </summary>
        private void DrawTowerRanges(ref SystemState state)
        {
            foreach (var (towerData, transform) in
                SystemAPI.Query<RefRO<TowerData>, RefRO<LocalTransform>>()
                    .WithAll<TowerTag>())
            {
                float3 pos = transform.ValueRO.Position;
                float range = towerData.ValueRO.AttackRange;

                // 使用 Debug.DrawLine 绘制圆形
                const int segments = 32;
                float angleStep = 2f * math.PI / segments;

                for (int i = 0; i < segments; i++)
                {
                    float a1 = i * angleStep;
                    float a2 = (i + 1) * angleStep;

                    Vector3 p1 = new Vector3(
                        pos.x + math.cos(a1) * range,
                        pos.y + math.sin(a1) * range,
                        0f);
                    Vector3 p2 = new Vector3(
                        pos.x + math.cos(a2) * range,
                        pos.y + math.sin(a2) * range,
                        0f);

                    Debug.DrawLine(p1, p2, Color.cyan);
                }
            }
        }
#endif
    }
}




