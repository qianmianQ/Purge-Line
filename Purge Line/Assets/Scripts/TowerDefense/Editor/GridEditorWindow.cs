#if UNITY_EDITOR
using System;
using System.IO;
using TowerDefense.Components;
using TowerDefense.Data;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// 网格编辑器窗口 — 可视化关卡地图编辑工具
    ///
    /// 功能：
    /// - 新建/加载/保存关卡配置（LevelConfigAsset）
    /// - 画笔模式：选择 CellType 后点击/拖拽绘制
    /// - 橡皮模式：擦除为 None
    /// - 填充模式：批量填充矩形区域
    /// - Undo/Redo 支持
    /// - 快捷键绑定
    /// - 实时颜色预览
    ///
    /// 入口：Window > Tower Defense > Grid Editor
    /// </summary>
    public class GridEditorWindow : EditorWindow
    {
        // ── 常量 ──────────────────────────────────────────────

        private const float MinCellDrawSize = 4f;
        private const float MaxCellDrawSize = 64f;
        private const float DefaultCellDrawSize = 20f;
        private const float ToolbarHeight = 220f;

        // ── 颜色定义 ─────────────────────────────────────────

        private static readonly Color ColorNone = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        private static readonly Color ColorSolid = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        private static readonly Color ColorWalkable = new Color(0.2f, 0.8f, 0.2f, 0.6f);
        private static readonly Color ColorPlaceable = new Color(0.2f, 0.4f, 0.9f, 0.6f);
        private static readonly Color ColorComposite = new Color(0.2f, 0.8f, 0.8f, 0.6f);
        private static readonly Color ColorGridLine = new Color(0.4f, 0.4f, 0.4f, 0.3f);
        private static readonly Color ColorHover = new Color(1f, 1f, 0f, 0.3f);

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

        private enum BrushMode
        {
            Paint,    // 画笔
            Erase,    // 橡皮
            Fill      // 矩形填充
        }

        // ── 菜单入口 ─────────────────────────────────────────

        [MenuItem("Window/Tower Defense/Grid Editor %#g")]
        public static void ShowWindow()
        {
            var window = GetWindow<GridEditorWindow>();
            window.titleContent = new GUIContent("Grid Editor", EditorGUIUtility.IconContent("Grid.Default").image);
            window.minSize = new Vector2(600, 400);
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
            EditorGUILayout.LabelField("Brush", GUILayout.Width(50));
            _brushMode = (BrushMode)GUILayout.Toolbar((int)_brushMode,
                new[] { "Paint", "Erase", "Fill" }, GUILayout.Height(24));
            EditorGUILayout.EndHorizontal();

            // ── 格子类型选择 ────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cell Type", GUILayout.Width(70));

            DrawCellTypeButton(CellType.None, "None", ColorNone);
            DrawCellTypeButton(CellType.Solid, "Solid", ColorSolid);
            DrawCellTypeButton(CellType.Walkable, "Walk", ColorWalkable);
            DrawCellTypeButton(CellType.Placeable, "Place", ColorPlaceable);
            DrawCellTypeButton(CellType.WalkableAndPlaceable, "W+P", ColorComposite);

            EditorGUILayout.EndHorizontal();

            // ── 缩放和操作按钮 ────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Zoom", GUILayout.Width(40));
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
                $"Brush: {_brushType} | Mode: {_brushMode}",
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
                // 背景
                EditorGUI.DrawRect(gridRect, new Color(0.1f, 0.1f, 0.1f, 1f));
            }

            GUI.BeginClip(gridRect);

            float totalWidth = _currentAsset.width * _cellDrawSize;
            float totalHeight = _currentAsset.height * _cellDrawSize;

            // 计算起始偏移（居中 + 平移）
            float offsetX = (gridRect.width - totalWidth) * 0.5f + _panOffset.x;
            float offsetY = (gridRect.height - totalHeight) * 0.5f + _panOffset.y;

            // 绘制格子
            for (int y = 0; y < _currentAsset.height; y++)
            {
                for (int x = 0; x < _currentAsset.width; x++)
                {
                    float drawX = offsetX + x * _cellDrawSize;
                    float drawY = offsetY + (_currentAsset.height - 1 - y) * _cellDrawSize; // 翻转Y轴

                    var cellRect = new Rect(drawX, drawY, _cellDrawSize, _cellDrawSize);

                    // 只绘制可见区域
                    if (cellRect.xMax < 0 || cellRect.xMin > gridRect.width ||
                        cellRect.yMax < 0 || cellRect.yMin > gridRect.height)
                        continue;

                    var cellType = _currentAsset.GetCellType(x, y);
                    Color cellColor = GetCellColor(cellType);

                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(cellRect, cellColor);

                        // 网格线
                        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 1), ColorGridLine);
                        EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 1, cellRect.height), ColorGridLine);

                        // 悬浮高亮
                        if (x == _hoverX && y == _hoverY)
                        {
                            EditorGUI.DrawRect(cellRect, ColorHover);
                        }

                        // 填充模式拖拽预览
                        if (_isFillDragging && _brushMode == BrushMode.Fill)
                        {
                            int minX = Mathf.Min(_fillStartX, _hoverX);
                            int maxX = Mathf.Max(_fillStartX, _hoverX);
                            int minY = Mathf.Min(_fillStartY, _hoverY);
                            int maxY = Mathf.Max(_fillStartY, _hoverY);

                            if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                            {
                                EditorGUI.DrawRect(cellRect, new Color(1f, 1f, 0f, 0.2f));
                            }
                        }
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

            // 左键绘制
            if (e.button == 0 && !_isPanning)
            {
                bool isValidCoord = _hoverX >= 0 && _hoverX < _currentAsset.width &&
                                    _hoverY >= 0 && _hoverY < _currentAsset.height;

                if (e.type == EventType.MouseDown && isValidCoord)
                {
                    if (_brushMode == BrushMode.Fill)
                    {
                        _isFillDragging = true;
                        _fillStartX = _hoverX;
                        _fillStartY = _hoverY;
                    }
                    else
                    {
                        PaintCell(_hoverX, _hoverY);
                    }
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && isValidCoord)
                {
                    if (_brushMode != BrushMode.Fill)
                    {
                        PaintCell(_hoverX, _hoverY);
                    }
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    if (_isFillDragging && _brushMode == BrushMode.Fill && isValidCoord)
                    {
                        Undo.RecordObject(_currentAsset, "Fill Rect");
                        CellType fillType = _brushMode == BrushMode.Erase ? CellType.None : _brushType;
                        _currentAsset.FillRect(_fillStartX, _fillStartY, _hoverX, _hoverY, fillType);
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
                    case KeyCode.R: _panOffset = Vector2.zero; e.Use(); break; // 重置视图
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
            Selection.activeObject = asset;
        }

        private void ExportToDataFolder()
        {
            if (_currentAsset == null) return;

            var config = _currentAsset.ToLevelConfig();
            string path = LevelConfigLoader.GetEditorFilePath(_currentAsset.levelId);
            LevelConfigLoader.SaveToFile(config, path);
            AssetDatabase.Refresh();
            Debug.Log($"[GridEditor] Exported to: {path}");
        }

        private void ExportToResources()
        {
            if (_currentAsset == null) return;

            var config = _currentAsset.ToLevelConfig();
            string path = LevelConfigLoader.GetResourcesFilePath(_currentAsset.levelId);
            LevelConfigLoader.SaveToFile(config, path);
            AssetDatabase.Refresh();
            Debug.Log($"[GridEditor] Exported to Resources: {path}");
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
            EditorUtility.SetDirty(_currentAsset);
            Debug.Log($"[GridEditor] Imported: {config.LevelId} ({config.Width}x{config.Height})");
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }
    }
}
#endif

