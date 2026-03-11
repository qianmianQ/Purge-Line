#if UNITY_EDITOR
using TowerDefense.Components;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// Scene 视图网格叠加层 — 在 Scene 窗口中显示格子可视化
    ///
    /// 当 GridEditorWindow 打开并选中 LevelConfigAsset 时，
    /// 在 Scene 视图中叠加绘制格子网格，便于空间参照。
    ///
    /// 通过 SceneView.duringSceneGui 回调挂接。
    /// </summary>
    [InitializeOnLoad]
    public static class GridSceneOverlay
    {
        // 颜色定义（与 GridEditorWindow 保持一致）
        private static readonly Color ColorNone = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        private static readonly Color ColorSolid = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color ColorWalkable = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        private static readonly Color ColorPlaceable = new Color(0.2f, 0.4f, 0.9f, 0.3f);
        private static readonly Color ColorComposite = new Color(0.2f, 0.8f, 0.8f, 0.3f);
        private static readonly Color ColorGridOutline = new Color(0.6f, 0.6f, 0.6f, 0.4f);

        private static LevelConfigAsset _activeAsset;
        private static bool _isEnabled = true;

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

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_isEnabled || _activeAsset == null) return;

            _activeAsset.EnsureCellsArray();

            float cellSize = _activeAsset.cellSize;
            float originX = _activeAsset.originX;
            float originY = _activeAsset.originY;
            int width = _activeAsset.width;
            int height = _activeAsset.height;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            // 绘制每个格子
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cellType = _activeAsset.GetCellType(x, y);
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

            // 坐标标注（每5格标一次，避免视觉杂乱）
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

