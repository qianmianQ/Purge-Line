// ============================================================================
// PurgeLine.Resource.Editor — ResourceMonitorWindow.cs
// Editor 资源监控窗口：实时查看引用计数、内存占用、加载状态
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PurgeLine.Resource.Diagnostics;
using PurgeLine.Resource.Internal;

namespace PurgeLine.Resource.Editor
{
    /// <summary>
    /// 资源监控 EditorWindow。
    /// 菜单路径: PurgeLine → Resource Monitor
    /// </summary>
    public sealed class ResourceMonitorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private readonly List<ResourceMetricEntry> _snapshots = new List<ResourceMetricEntry>(256);
        private bool _autoRefresh = true;
        private float _refreshInterval = 1f;
        private double _lastRefreshTime;

        // 排序
        private enum SortColumn { Address, RefCount, LoadTime, InLru }
        private SortColumn _sortColumn = SortColumn.Address;
        private bool _sortAscending = true;

        [MenuItem("PurgeLine/Resource Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ResourceMonitorWindow>("Resource Monitor");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawResourceTable();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                RefreshData();
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton);
            _refreshInterval = EditorGUILayout.Slider("Interval", _refreshInterval, 0.1f, 5f, GUILayout.Width(250));

            if (GUILayout.Button("Refresh Now", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                RefreshData();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            var mgr = GetResourceManager();
            if (mgr == null)
            {
                EditorGUILayout.HelpBox("ResourceManager not available. Enter Play Mode first.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            var metrics = mgr.Metrics;
            DrawStatBox("Cached", mgr.CachedCount.ToString());
            DrawStatBox("LRU", mgr.LruCount.ToString());
            DrawStatBox("Pending", mgr.PendingLoadCount.ToString());
            DrawStatBox("Loads", metrics?.TotalLoadRequests.ToString() ?? "0");
            DrawStatBox("Hits", metrics?.TotalCacheHits.ToString() ?? "0");
            DrawStatBox("Misses", metrics?.TotalCacheMisses.ToString() ?? "0");
            DrawStatBox("Failures", metrics?.TotalLoadFailures.ToString() ?? "0");
            DrawStatBox("Pool Hits", metrics?.TotalPoolHits.ToString() ?? "0");

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawStatBox(string label, string value)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(70));
            GUILayout.Label(value, EditorStyles.boldLabel);
            GUILayout.Label(label, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawResourceTable()
        {
            // 表头
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Address", EditorStyles.toolbarButton, GUILayout.MinWidth(200)))
                ToggleSort(SortColumn.Address);
            if (GUILayout.Button("Refs", EditorStyles.toolbarButton, GUILayout.Width(50)))
                ToggleSort(SortColumn.RefCount);
            if (GUILayout.Button("Load ms", EditorStyles.toolbarButton, GUILayout.Width(70)))
                ToggleSort(SortColumn.LoadTime);
            if (GUILayout.Button("In LRU", EditorStyles.toolbarButton, GUILayout.Width(60)))
                ToggleSort(SortColumn.InLru);
            EditorGUILayout.EndHorizontal();

            // 表内容
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            SortSnapshots();

            for (int i = 0; i < _snapshots.Count; i++)
            {
                var entry = _snapshots[i];
                EditorGUILayout.BeginHorizontal();

                // 引用计数为 0 且在 LRU 中的条目用黄色标记
                if (entry.InLru)
                    GUI.color = new Color(1f, 0.9f, 0.6f);
                else if (entry.RefCount == 0)
                    GUI.color = Color.red;
                else
                    GUI.color = Color.white;

                GUILayout.Label(entry.Address, GUILayout.MinWidth(200));
                GUILayout.Label(entry.RefCount.ToString(), GUILayout.Width(50));
                GUILayout.Label(entry.LoadTimeMs.ToString("F1"), GUILayout.Width(70));
                GUILayout.Label(entry.InLru ? "✓" : "", GUILayout.Width(60));

                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ToggleSort(SortColumn column)
        {
            if (_sortColumn == column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }
        }

        private void SortSnapshots()
        {
            _snapshots.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    SortColumn.Address => string.Compare(a.Address, b.Address, System.StringComparison.Ordinal),
                    SortColumn.RefCount => a.RefCount.CompareTo(b.RefCount),
                    SortColumn.LoadTime => a.LoadTimeMs.CompareTo(b.LoadTimeMs),
                    SortColumn.InLru => a.InLru.CompareTo(b.InLru),
                    _ => 0,
                };
                return _sortAscending ? cmp : -cmp;
            });
        }

        private void RefreshData()
        {
            var mgr = GetResourceManager();
            if (mgr == null) return;

            mgr.CollectMetricSnapshots(_snapshots);
        }

        private static ResourceManager GetResourceManager()
        {
            return ResourceManagerResolver.TryGet();
        }
    }
}
#endif


