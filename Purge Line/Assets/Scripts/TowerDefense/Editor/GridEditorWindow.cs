#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TowerDefense.Components;
using TowerDefense.Data;
using TowerDefense.Utilities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 关卡编辑器窗口 — 可视化关卡地图编辑工具
    ///
    /// 功能：
    /// - 新建/加载/保存关卡配置（LevelConfigAsset）
    /// - 画笔/橡皮/填充模式绘制
    /// - 目标点(GoalPoint)/出生点(SpawnPoint) 可视化编辑
    /// - 流场烘焙与可视化
    /// - Undo/Redo 支持
    /// - 快捷键绑定
    ///
    /// 入口：Window > Tower Defense > 关卡编辑器
    /// </summary>
    public class GridEditorWindow : EditorWindow
    {
        // ── 常量 ──────────────────────────────────────────────

        private const float MinCellDrawSize = 4f;
        private const float MaxCellDrawSize = 64f;
        private const float DefaultCellDrawSize = 20f;

        // ── 颜色定义 ─────────────────────────────────────────

        private static readonly Color ColorNone = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        private static readonly Color ColorSolid = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        private static readonly Color ColorWalkable = new Color(0.2f, 0.8f, 0.2f, 0.6f);
        private static readonly Color ColorPlaceable = new Color(0.2f, 0.4f, 0.9f, 0.6f);
        private static readonly Color ColorComposite = new Color(0.2f, 0.8f, 0.8f, 0.6f);
        private static readonly Color ColorGridLine = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        private static readonly Color ColorHover = new Color(1f, 1f, 0f, 0.3f);
        private static readonly Color ColorGoalMarker = new Color(1f, 0.2f, 0.2f, 0.9f);
        private static readonly Color ColorSpawnMarker = new Color(0.2f, 0.5f, 1f, 0.9f);
        private static readonly Color ColorFlowArrow = new Color(1f, 0.6f, 0f, 0.85f);

        // ── 编辑状态 ─────────────────────────────────────────

        private LevelConfigAsset _currentAsset;
        private CellType _brushType = CellType.Walkable;
        private BrushMode _brushMode = BrushMode.Paint;
        private float _cellDrawSize = DefaultCellDrawSize;
        private Vector2 _scrollPosition;
        private Vector2 _panOffset;
        private bool _isPanning;
        private Vector2 _lastMousePos;
        private int _hoverX = -1;
        private int _hoverY = -1;

        // 填充模式状态
        private bool _isFillDragging;
        private int _fillStartX;
        private int _fillStartY;

        // 流场编辑器状态
        private byte[] _editorBakedDirections;
        private bool _showFlowField;
        private bool _saveBakeToConfig;

        // 点位快速查找缓存
        private readonly HashSet<long> _goalPointSet = new HashSet<long>();
        private readonly HashSet<long> _spawnPointSet = new HashSet<long>();

        private enum BrushMode
        {
            Paint,         // 画笔
            Erase,         // 橡皮
            Fill,          // 矩形填充
            SetGoalPoint,  // 设置目标点
            SetSpawnPoint  // 设置出生点
        }

        // ── 菜单入口 ─────────────────────────────────────────

        [MenuItem("Window/Tower Defense/关卡编辑器 %#g")]
        public static void ShowWindow()
        {
            var window = GetWindow<GridEditorWindow>();
            window.titleContent = new GUIContent("关卡编辑器", EditorGUIUtility.IconContent("Grid.Default").image);
            window.minSize = new Vector2(600, 400);
        }

        // ── 辅助方法 ─────────────────────────────────────────

        private static long PackCoord(int x, int y) => ((long)x << 32) | (uint)y;

        private void RefreshPointSets()
        {
            _goalPointSet.Clear();
            _spawnPointSet.Clear();
            if (_currentAsset?.goalPoints != null)
                foreach (var gp in _currentAsset.goalPoints)
                    _goalPointSet.Add(PackCoord(Mathf.FloorToInt(gp.x), Mathf.FloorToInt(gp.y)));
            if (_currentAsset?.spawnPoints != null)
                foreach (var sp in _currentAsset.spawnPoints)
                    _spawnPointSet.Add(PackCoord(Mathf.FloorToInt(sp.x), Mathf.FloorToInt(sp.y)));
        }

        // ── GUI 绘制 ─────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            DrawGrid();
            HandleInput();
        }

        /// <summary>绘制工具栏</summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── 资产选择行 ────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level Config Asset", GUILayout.Width(120));
            var newAsset = (LevelConfigAsset)EditorGUILayout.ObjectField(
                _currentAsset, typeof(LevelConfigAsset), false);
            if (newAsset != _currentAsset)
            {
                _currentAsset = newAsset;
                _editorBakedDirections = null;
                _showFlowField = false;
                RefreshPointSets();
                GridSceneOverlay.SetActiveAsset(_currentAsset);
                Repaint();
            }

            if (GUILayout.Button("New", GUILayout.Width(50)))
            {
                CreateNewAsset();
            }
            EditorGUILayout.EndHorizontal();

            if (_currentAsset == null)
            {
                EditorGUILayout.HelpBox("请选择或创建一个 Level Config Asset", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(4);

            // ── 关卡信息行 ────
            EditorGUI.BeginChangeCheck();
            _currentAsset.levelId = EditorGUILayout.TextField("Level ID", _currentAsset.levelId);
            _currentAsset.displayName = EditorGUILayout.TextField("Display Name", _currentAsset.displayName);

            EditorGUILayout.BeginHorizontal();
            int newWidth = EditorGUILayout.IntField("Width", _currentAsset.width);
            int newHeight = EditorGUILayout.IntField("Height", _currentAsset.height);
            EditorGUILayout.EndHorizontal();

            if (newWidth != _currentAsset.width || newHeight != _currentAsset.height)
            {
                Undo.RecordObject(_currentAsset, "Resize Grid");
                _currentAsset.width = Mathf.Max(1, newWidth);
                _currentAsset.height = Mathf.Max(1, newHeight);
                _currentAsset.EnsureCellsArray();
                _editorBakedDirections = null;
                EditorUtility.SetDirty(_currentAsset);
            }

            _currentAsset.cellSize = EditorGUILayout.FloatField("Cell Size", _currentAsset.cellSize);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_currentAsset);
            }

            EditorGUILayout.Space(4);

            // ── 画笔工具行 ────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("工具", GUILayout.Width(50));
            _brushMode = (BrushMode)GUILayout.Toolbar((int)_brushMode,
                new[] { "画笔", "橡皮", "填充", "目标点", "出生点" }, GUILayout.Height(24));
            EditorGUILayout.EndHorizontal();

            // ── 格子类型选择（仅画笔/填充模式）────
            if (_brushMode == BrushMode.Paint || _brushMode == BrushMode.Fill)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("格子类型", GUILayout.Width(70));

                DrawCellTypeButton(CellType.None, "None", ColorNone);
                DrawCellTypeButton(CellType.Solid, "Solid", ColorSolid);
                DrawCellTypeButton(CellType.Walkable, "Walk", ColorWalkable);
                DrawCellTypeButton(CellType.Placeable, "Place", ColorPlaceable);
                DrawCellTypeButton(CellType.WalkableAndPlaceable, "W+P", ColorComposite);

                EditorGUILayout.EndHorizontal();
            }

            // ── 缩放和操作按钮 ────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("缩放", GUILayout.Width(40));
            _cellDrawSize = EditorGUILayout.Slider(_cellDrawSize, MinCellDrawSize, MaxCellDrawSize);

            if (GUILayout.Button("Fill All", GUILayout.Width(60)))
            {
                Undo.RecordObject(_currentAsset, "Fill All");
                _currentAsset.FillAll(_brushType);
                EditorUtility.SetDirty(_currentAsset);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                Undo.RecordObject(_currentAsset, "Clear Grid");
                _currentAsset.FillAll(CellType.None);
                EditorUtility.SetDirty(_currentAsset);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── 流场工具行 ────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("流场工具", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            bool hasGoals = _currentAsset.goalPoints != null && _currentAsset.goalPoints.Length > 0;
            EditorGUI.BeginDisabledGroup(!hasGoals);
            if (GUILayout.Button("Bake 流场", GUILayout.Height(24), GUILayout.Width(100)))
            {
                BakeFlowField();
            }
            EditorGUI.EndDisabledGroup();

            if (!hasGoals)
            {
                EditorGUILayout.HelpBox("请先设置目标点", MessageType.None);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(_editorBakedDirections == null);
            _showFlowField = EditorGUILayout.ToggleLeft("显示流场", _showFlowField, GUILayout.Width(80));
            EditorGUI.EndDisabledGroup();

            _saveBakeToConfig = EditorGUILayout.ToggleLeft("保存Bake结果到LevelConfig", _saveBakeToConfig);

            EditorGUILayout.EndHorizontal();

            if (_editorBakedDirections != null)
            {
                EditorGUILayout.LabelField(
                    $"流场状态: 已烘焙 ({_editorBakedDirections.Length} cells)",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // ── 导出按钮 ────
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export .bytes (Data)", GUILayout.Height(24)))
            {
                ExportToDataFolder();
            }

            if (GUILayout.Button("Export .bytes (Resources)", GUILayout.Height(24)))
            {
                ExportToResources();
            }

            if (GUILayout.Button("Import .bytes", GUILayout.Height(24)))
            {
                ImportFromBytes();
            }

            EditorGUILayout.EndHorizontal();

            // ── 状态信息 ────
            EditorGUILayout.LabelField(
                $"Grid: {_currentAsset.width}×{_currentAsset.height} | " +
                $"Hover: ({_hoverX}, {_hoverY}) | " +
                $"Goals: {_currentAsset.goalPoints?.Length ?? 0} | " +
                $"Spawns: {_currentAsset.spawnPoints?.Length ?? 0} | " +
                $"Mode: {_brushMode}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        /// <summary>绘制格子类型选择按钮</summary>
        private void DrawCellTypeButton(CellType type, string label, Color color)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _brushType == type ? Color.white : color;
            var style = _brushType == type ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(label, style, GUILayout.Width(50), GUILayout.Height(20)))
            {
                _brushType = type;
                if (_brushMode == BrushMode.Erase)
                    _brushMode = BrushMode.Paint;
            }
            GUI.backgroundColor = prevBg;
        }

        /// <summary>绘制网格画布</summary>
        private void DrawGrid()
        {
            if (_currentAsset == null) return;

            _currentAsset.EnsureCellsArray();

            var gridRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(gridRect, new Color(0.1f, 0.1f, 0.1f, 1f));
            }

            GUI.BeginClip(gridRect);

            float totalWidth = _currentAsset.width * _cellDrawSize;
            float totalHeight = _currentAsset.height * _cellDrawSize;

            float offsetX = (gridRect.width - totalWidth) * 0.5f + _panOffset.x;
            float offsetY = (gridRect.height - totalHeight) * 0.5f + _panOffset.y;

            for (int y = 0; y < _currentAsset.height; y++)
            {
                for (int x = 0; x < _currentAsset.width; x++)
                {
                    float drawX = offsetX + x * _cellDrawSize;
                    float drawY = offsetY + (_currentAsset.height - 1 - y) * _cellDrawSize;

                    var cellRect = new Rect(drawX, drawY, _cellDrawSize, _cellDrawSize);

                    if (cellRect.xMax < 0 || cellRect.xMin > gridRect.width ||
                        cellRect.yMax < 0 || cellRect.yMin > gridRect.height)
                        continue;

                    if (Event.current.type != EventType.Repaint) continue;

                    var cellType = _currentAsset.GetCellType(x, y);
                    EditorGUI.DrawRect(cellRect, GetCellColor(cellType));

                    // 网格线
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1), ColorGridLine);
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1, cellRect.height), ColorGridLine);

                    // 目标点标记
                    if (_goalPointSet.Contains(PackCoord(x, y)))
                        DrawMarker(cellRect, ColorGoalMarker, "G");

                    // 出生点标记
                    if (_spawnPointSet.Contains(PackCoord(x, y)))
                        DrawMarker(cellRect, ColorSpawnMarker, "S");

                    // 流场方向指示
                    if (_showFlowField && _editorBakedDirections != null)
                    {
                        int index = y * _currentAsset.width + x;
                        if (index < _editorBakedDirections.Length)
                            DrawFlowFieldIndicator(drawX, drawY, _cellDrawSize, _editorBakedDirections[index]);
                    }

                    // 悬浮高亮
                    if (x == _hoverX && y == _hoverY)
                        EditorGUI.DrawRect(cellRect, ColorHover);

                    // 填充模式拖拽预览
                    if (_isFillDragging && _brushMode == BrushMode.Fill)
                    {
                        int minX = Mathf.Min(_fillStartX, _hoverX);
                        int maxX = Mathf.Max(_fillStartX, _hoverX);
                        int minY = Mathf.Min(_fillStartY, _hoverY);
                        int maxY = Mathf.Max(_fillStartY, _hoverY);

                        if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                            EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 0f, 0.2f));
                    }
                }
            }

            // 更新悬浮坐标
            if (Event.current.type == EventType.MouseMove ||
                Event.current.type == EventType.MouseDrag)
            {
                Vector2 localMouse = Event.current.mousePosition;
                _hoverX = Mathf.FloorToInt((localMouse.x - offsetX) / _cellDrawSize);
                _hoverY = _currentAsset.height - 1 - Mathf.FloorToInt((localMouse.y - offsetY) / _cellDrawSize);
                Repaint();
            }

            GUI.EndClip();
        }

        /// <summary>绘制目标点/出生点标记</summary>
        private void DrawMarker(Rect cellRect, Color color, string label)
        {
            float markerSize = Mathf.Max(4f, _cellDrawSize * 0.5f);
            float cx = cellRect.center.x;
            float cy = cellRect.center.y;

            EditorGUI.DrawRect(
                new Rect(cx - markerSize * 0.5f, cy - markerSize * 0.5f, markerSize, markerSize),
                color);

            if (_cellDrawSize >= 16f)
            {
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = Mathf.Max(8, (int)(_cellDrawSize * 0.3f))
                };
                GUI.Label(cellRect, label, style);
            }
        }

        /// <summary>绘制流场方向指示器</summary>
        private static void DrawFlowFieldIndicator(float drawX, float drawY, float size, byte direction)
        {
            if (!FlowFieldMath.IsValidDirection(direction)) return;

            float cx = drawX + size * 0.5f;
            float cy = drawY + size * 0.5f;

            int2 offset = FlowFieldMath.DirectionToOffset(direction);
            float dx = offset.x;
            float dy = -offset.y; // Y翻转

            float radius = size * 0.3f;
            float dotSize = Mathf.Max(2f, size * 0.15f);

            float tipX = cx + dx * radius;
            float tipY = cy + dy * radius;

            EditorGUI.DrawRect(
                new Rect(tipX - dotSize * 0.5f, tipY - dotSize * 0.5f, dotSize, dotSize),
                ColorFlowArrow);

            // 中间点（格子够大时画线段近似）
            if (size >= 12f)
            {
                float midX = (cx + tipX) * 0.5f;
                float midY = (cy + tipY) * 0.5f;
                float midDot = dotSize * 0.6f;
                EditorGUI.DrawRect(
                    new Rect(midX - midDot * 0.5f, midY - midDot * 0.5f, midDot, midDot),
                    ColorFlowArrow);
            }
        }

        /// <summary>处理输入事件</summary>
        private void HandleInput()
        {
            if (_currentAsset == null) return;

            var e = Event.current;

            // 鼠标滚轮缩放
            if (e.type == EventType.ScrollWheel)
            {
                _cellDrawSize = Mathf.Clamp(_cellDrawSize - e.delta.y * 0.5f, MinCellDrawSize, MaxCellDrawSize);
                e.Use();
                Repaint();
            }

            // 中键平移
            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _isPanning = true;
                _lastMousePos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isPanning)
            {
                _panOffset += e.mousePosition - _lastMousePos;
                _lastMousePos = e.mousePosition;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 2)
            {
                _isPanning = false;
                e.Use();
            }

            // 左键操作
            if (e.button == 0 && !_isPanning)
            {
                bool isValidCoord = _hoverX >= 0 && _hoverX < _currentAsset.width &&
                                    _hoverY >= 0 && _hoverY < _currentAsset.height;

                if (e.type == EventType.MouseDown && isValidCoord)
                {
                    switch (_brushMode)
                    {
                        case BrushMode.Fill:
                            _isFillDragging = true;
                            _fillStartX = _hoverX;
                            _fillStartY = _hoverY;
                            break;
                        case BrushMode.SetGoalPoint:
                            ToggleGoalPoint(_hoverX, _hoverY);
                            break;
                        case BrushMode.SetSpawnPoint:
                            ToggleSpawnPoint(_hoverX, _hoverY);
                            break;
                        default:
                            PaintCell(_hoverX, _hoverY);
                            break;
                    }
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && isValidCoord)
                {
                    if (_brushMode == BrushMode.Paint || _brushMode == BrushMode.Erase)
                        PaintCell(_hoverX, _hoverY);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    if (_isFillDragging && _brushMode == BrushMode.Fill && isValidCoord)
                    {
                        Undo.RecordObject(_currentAsset, "Fill Rect");
                        _currentAsset.FillRect(_fillStartX, _fillStartY, _hoverX, _hoverY, _brushType);
                        EditorUtility.SetDirty(_currentAsset);
                    }
                    _isFillDragging = false;
                    e.Use();
                }
            }

            // 快捷键
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Alpha1: _brushType = CellType.None; e.Use(); break;
                    case KeyCode.Alpha2: _brushType = CellType.Solid; e.Use(); break;
                    case KeyCode.Alpha3: _brushType = CellType.Walkable; e.Use(); break;
                    case KeyCode.Alpha4: _brushType = CellType.Placeable; e.Use(); break;
                    case KeyCode.Alpha5: _brushType = CellType.WalkableAndPlaceable; e.Use(); break;
                    case KeyCode.B: _brushMode = BrushMode.Paint; e.Use(); break;
                    case KeyCode.E: _brushMode = BrushMode.Erase; e.Use(); break;
                    case KeyCode.F: _brushMode = BrushMode.Fill; e.Use(); break;
                    case KeyCode.G: _brushMode = BrushMode.SetGoalPoint; e.Use(); break;
                    case KeyCode.R: _panOffset = Vector2.zero; e.Use(); break;
                }
                Repaint();
            }
        }

        /// <summary>绘制单个格子</summary>
        private void PaintCell(int x, int y)
        {
            CellType type = _brushMode == BrushMode.Erase ? CellType.None : _brushType;
            if (_currentAsset.GetCellType(x, y) == type) return;

            Undo.RecordObject(_currentAsset, "Paint Cell");
            _currentAsset.SetCellType(x, y, type);
            EditorUtility.SetDirty(_currentAsset);
            Repaint();
        }

        // ── 目标点/出生点操作 ──────────────────────────────────

        private void ToggleGoalPoint(int x, int y)
        {
            var list = new List<Vector2>(_currentAsset.goalPoints ?? Array.Empty<Vector2>());
            int idx = list.FindIndex(p => Mathf.FloorToInt(p.x) == x && Mathf.FloorToInt(p.y) == y);

            Undo.RecordObject(_currentAsset, "Toggle Goal Point");
            if (idx >= 0) list.RemoveAt(idx);
            else list.Add(new Vector2(x, y));

            _currentAsset.goalPoints = list.ToArray();
            EditorUtility.SetDirty(_currentAsset);
            RefreshPointSets();
            GridSceneOverlay.SetActiveAsset(_currentAsset);
            Repaint();
        }

        private void ToggleSpawnPoint(int x, int y)
        {
            var list = new List<Vector2>(_currentAsset.spawnPoints ?? Array.Empty<Vector2>());
            int idx = list.FindIndex(p => Mathf.FloorToInt(p.x) == x && Mathf.FloorToInt(p.y) == y);

            Undo.RecordObject(_currentAsset, "Toggle Spawn Point");
            if (idx >= 0) list.RemoveAt(idx);
            else list.Add(new Vector2(x, y));

            _currentAsset.spawnPoints = list.ToArray();
            EditorUtility.SetDirty(_currentAsset);
            RefreshPointSets();
            GridSceneOverlay.SetActiveAsset(_currentAsset);
            Repaint();
        }

        // ── 流场烘焙 ─────────────────────────────────────────

        private void BakeFlowField()
        {
            if (_currentAsset == null) return;
            if (_currentAsset.goalPoints == null || _currentAsset.goalPoints.Length == 0)
            {
                EditorUtility.DisplayDialog("Flow Field Bake", "请先设置至少一个目标点", "OK");
                return;
            }

            _currentAsset.EnsureCellsArray();

            var goalList = new List<int2>();
            for (int i = 0; i < _currentAsset.goalPoints.Length; i++)
            {
                var gp = _currentAsset.goalPoints[i];
                int gx = Mathf.FloorToInt(gp.x);
                int gy = Mathf.FloorToInt(gp.y);
                if (gx >= 0 && gx < _currentAsset.width && gy >= 0 && gy < _currentAsset.height)
                    goalList.Add(new int2(gx, gy));
                else
                    Debug.LogWarning($"[关卡编辑器] Goal point ({gp.x}, {gp.y}) out of bounds, skipped");
            }

            if (goalList.Count == 0)
            {
                EditorUtility.DisplayDialog("Flow Field Bake", "所有目标点都超出网格范围", "OK");
                return;
            }

            try
            {
                _editorBakedDirections = FlowFieldBaker.BakeInEditor(
                    _currentAsset.cells, _currentAsset.width, _currentAsset.height,
                    goalList.ToArray());
                _showFlowField = true;

                GridSceneOverlay.SetFlowFieldData(
                    _editorBakedDirections, _currentAsset.width, _currentAsset.height);

                if (_saveBakeToConfig)
                {
                    Undo.RecordObject(_currentAsset, "Save Baked Flow Field");
                    _currentAsset.bakedFlowFieldDirections = (byte[])_editorBakedDirections.Clone();
                    var tempConfig = _currentAsset.ToLevelConfig();
                    _currentAsset.bakedFlowFieldDataHash = tempConfig.ComputeFlowFieldDataHash();
                    _currentAsset.bakedFlowFieldVersion = LevelConfig.FlowFieldAlgorithmVersion;
                    EditorUtility.SetDirty(_currentAsset);
                    Debug.Log("[关卡编辑器] Flow field baked and saved to config");
                }
                else
                {
                    Debug.Log("[关卡编辑器] Flow field baked (not saved to config)");
                }

                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Flow Field Bake Error", $"烘焙失败: {ex.Message}", "OK");
                Debug.LogException(ex);
            }
        }

        /// <summary>获取格子类型对应的显示颜色</summary>
        private static Color GetCellColor(CellType type)
        {
            return type switch
            {
                CellType.None => ColorNone,
                CellType.Solid => ColorSolid,
                CellType.Walkable => ColorWalkable,
                CellType.Placeable => ColorPlaceable,
                CellType.WalkableAndPlaceable => ColorComposite,
                _ => ColorNone
            };
        }

        // ── 资产操作 ─────────────────────────────────────────

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Level Config Asset",
                "NewLevelConfig",
                "asset",
                "Choose location for new Level Config Asset");

            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<LevelConfigAsset>();
            asset.EnsureCellsArray();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _currentAsset = asset;
            _editorBakedDirections = null;
            RefreshPointSets();
            Selection.activeObject = asset;
        }

        private void ExportToDataFolder()
        {
            if (_currentAsset == null) return;

            var config = _currentAsset.ToLevelConfig();
            string path = LevelConfigLoader.GetEditorFilePath(_currentAsset.levelId);
            LevelConfigLoader.SaveToFile(config, path);
            AssetDatabase.Refresh();
            Debug.Log($"[关卡编辑器] Exported to: {path}");
        }

        private void ExportToResources()
        {
            if (_currentAsset == null) return;

            var config = _currentAsset.ToLevelConfig();
            string path = LevelConfigLoader.GetResourcesFilePath(_currentAsset.levelId);
            LevelConfigLoader.SaveToFile(config, path);
            AssetDatabase.Refresh();
            Debug.Log($"[关卡编辑器] Exported to Resources: {path}");
        }

        private void ImportFromBytes()
        {
            string path = EditorUtility.OpenFilePanel("Import Level Config", "Assets", "bytes");
            if (string.IsNullOrEmpty(path)) return;

            var config = LevelConfigLoader.LoadFromFile(path);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Import Error", "Failed to load level config from file.", "OK");
                return;
            }

            if (_currentAsset == null)
            {
                CreateNewAsset();
                if (_currentAsset == null) return;
            }

            Undo.RecordObject(_currentAsset, "Import Level Config");
            _currentAsset.FromLevelConfig(config);
            _editorBakedDirections = null;
            RefreshPointSets();
            EditorUtility.SetDirty(_currentAsset);
            Debug.Log($"[关卡编辑器] Imported: {config.LevelId} ({config.Width}x{config.Height})");
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            RefreshPointSets();
        }
    }
}
#endif

