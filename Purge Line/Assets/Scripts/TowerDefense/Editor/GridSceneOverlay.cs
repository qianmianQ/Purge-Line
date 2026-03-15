#if UNITY_EDITOR
using TowerDefense.Components;
using TowerDefense.Utilities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Scene 视图网格叠加层 — 在 Scene 窗口中显示格子可视化
    ///
    /// 功能：
    /// - 格子类型颜色渲染
    /// - 目标点(红)/出生点(蓝)标记
    /// - 流场方向箭头可视化
    ///
    /// 通过 SceneView.duringSceneGui 回调挂接。
    /// </summary>
    [InitializeOnLoad]
    public static class GridSceneOverlay
    {
        private static readonly Color ColorNone = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        private static readonly Color ColorSolid = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color ColorWalkable = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        private static readonly Color ColorPlaceable = new Color(0.2f, 0.4f, 0.9f, 0.3f);
        private static readonly Color ColorComposite = new Color(0.2f, 0.8f, 0.8f, 0.3f);
        private static readonly Color ColorGridOutline = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        private static readonly Color ColorGoal = new Color(1f, 0.2f, 0.2f, 0.8f);
        private static readonly Color ColorSpawn = new Color(0.2f, 0.5f, 1f, 0.8f);
        private static readonly Color ColorFlowArrow = new Color(1f, 0.6f, 0f, 0.7f);

        private static LevelConfigAsset _activeAsset;
        private static bool _isEnabled = true;

        // 流场数据
        private static byte[] _flowFieldDirections;
        private static int _flowFieldWidth;
        private static int _flowFieldHeight;
        private static bool _showFlowField;

        static GridSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>设置当前显示的关卡资产</summary>
        public static void SetActiveAsset(LevelConfigAsset asset)
        {
            _activeAsset = asset;
            SceneView.RepaintAll();
        }

        /// <summary>启用/禁用叠加层</summary>
        public static void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            SceneView.RepaintAll();
        }

        /// <summary>设置流场可视化数据</summary>
        public static void SetFlowFieldData(byte[] directions, int width, int height)
        {
            _flowFieldDirections = directions;
            _flowFieldWidth = width;
            _flowFieldHeight = height;
            _showFlowField = directions != null;
            SceneView.RepaintAll();
        }

        /// <summary>设置流场是否可见</summary>
        public static void SetFlowFieldVisible(bool visible)
        {
            _showFlowField = visible;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_isEnabled || _activeAsset == null) return;
            var config = _activeAsset.LevelConfig;
            if (config == null) return;

            _activeAsset.EnsureCellsArray();

            float cellSize = config.CellSize;
            float originX = config.OriginX;
            float originY = config.OriginY;
            int width = config.Width;
            int height = config.Height;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 绘制每个格子
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cellType = config.GetCellType(x, y);
                    Color color = GetColor(cellType);

                    Vector3 bottomLeft = new Vector3(
                        originX + x * cellSize,
                        originY + y * cellSize,
                        0f);

                    Vector3[] verts = new Vector3[4]
                    {
                        bottomLeft,
                        bottomLeft + new Vector3(cellSize, 0f, 0f),
                        bottomLeft + new Vector3(cellSize, cellSize, 0f),
                        bottomLeft + new Vector3(0f, cellSize, 0f)
                    };

                    Handles.DrawSolidRectangleWithOutline(verts, color, ColorGridOutline);

                    // 流场箭头
                    if (_showFlowField && _flowFieldDirections != null)
                    {
                        int index = y * width + x;
                        if (index < _flowFieldDirections.Length &&
                            FlowFieldMath.IsValidDirection(_flowFieldDirections[index]))
                        {
                            DrawFlowArrow(bottomLeft, cellSize, _flowFieldDirections[index]);
                        }
                    }
                }
            }

            // 绘制目标点标记
            if (config.GoalPoints != null)
            {
                foreach (var gp in config.GoalPoints)
                {
                    int gx = Mathf.FloorToInt(gp.x);
                    int gy = Mathf.FloorToInt(gp.y);
                    if (gx >= 0 && gx < width && gy >= 0 && gy < height)
                        DrawPointMarker(originX, originY, cellSize, gx, gy, ColorGoal, "G");
                }
            }

            // 绘制出生点标记
            if (config.SpawnPoints != null)
            {
                foreach (var sp in config.SpawnPoints)
                {
                    int sx = Mathf.FloorToInt(sp.x);
                    int sy = Mathf.FloorToInt(sp.y);
                    if (sx >= 0 && sx < width && sy >= 0 && sy < height)
                        DrawPointMarker(originX, originY, cellSize, sx, sy, ColorSpawn, "S");
                }
            }

            // 绘制外边框
            Vector3 mapOrigin = new Vector3(originX, originY, 0f);
            Vector3 mapEnd = new Vector3(originX + width * cellSize, originY + height * cellSize, 0f);

            Handles.color = Color.yellow;
            Vector3[] outerVerts = new Vector3[4]
            {
                mapOrigin,
                new Vector3(mapEnd.x, mapOrigin.y, 0f),
                mapEnd,
                new Vector3(mapOrigin.x, mapEnd.y, 0f)
            };
            Handles.DrawSolidRectangleWithOutline(outerVerts, Color.clear, Color.yellow);

            // 坐标标注
            Handles.color = Color.white;
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                fontSize = 9
            };

            for (int x = 0; x < width; x += 5)
            {
                Vector3 pos = new Vector3(originX + x * cellSize + cellSize * 0.5f,
                    originY - cellSize * 0.3f, 0f);
                Handles.Label(pos, x.ToString(), labelStyle);
            }

            for (int y = 0; y < height; y += 5)
            {
                Vector3 pos = new Vector3(originX - cellSize * 0.5f,
                    originY + y * cellSize + cellSize * 0.5f, 0f);
                Handles.Label(pos, y.ToString(), labelStyle);
            }
        }

        /// <summary>在Scene视图中绘制流场方向箭头</summary>
        private static void DrawFlowArrow(Vector3 bottomLeft, float cellSize, byte direction)
        {
            int2 offset = FlowFieldMath.DirectionToOffset(direction);
            float2 dir = new float2(offset.x, offset.y);
            float len = math.length(dir);
            if (len < 0.01f) return;
            dir /= len;

            Vector3 center = bottomLeft + new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
            float arrowLen = cellSize * 0.35f;

            Vector3 tip = center + new Vector3(dir.x, dir.y, 0f) * arrowLen;
            Vector3 tail = center - new Vector3(dir.x, dir.y, 0f) * arrowLen * 0.3f;

            Handles.color = ColorFlowArrow;
            Handles.DrawAAPolyLine(2f, tail, tip);

            // 箭头头部
            float headSize = cellSize * 0.12f;
            Vector3 perpDir = new Vector3(-dir.y, dir.x, 0f);
            Vector3 headLeft = tip - new Vector3(dir.x, dir.y, 0f) * headSize + perpDir * headSize * 0.6f;
            Vector3 headRight = tip - new Vector3(dir.x, dir.y, 0f) * headSize - perpDir * headSize * 0.6f;

            Handles.DrawAAPolyLine(2f, headLeft, tip);
            Handles.DrawAAPolyLine(2f, headRight, tip);
        }

        /// <summary>绘制目标点/出生点圆形标记</summary>
        private static void DrawPointMarker(float originX, float originY, float cellSize,
            int gx, int gy, Color color, string label)
        {
            Vector3 center = new Vector3(
                originX + gx * cellSize + cellSize * 0.5f,
                originY + gy * cellSize + cellSize * 0.5f,
                0f);

            float markerSize = cellSize * 0.35f;
            Handles.color = color;
            Handles.DrawSolidDisc(center, Vector3.forward, markerSize);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(10, (int)(cellSize * 0.45f))
            };
            Handles.Label(center + new Vector3(-cellSize * 0.12f, -cellSize * 0.12f, 0f), label, style);
        }

        private static Color GetColor(CellType type)
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
    }
}
#endif

