#if UNITY_EDITOR
using TowerDefense.Components;
using TowerDefense.Data;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    /// <summary>
    /// LevelConfigAsset 自定义 Inspector
    ///
    /// 功能：
    /// - 显示格子统计信息
    /// - 快捷导出按钮
    /// - 在 Scene View 显示叠加层
    /// - 快速打开 Grid Editor 窗口
    /// </summary>
    [CustomEditor(typeof(LevelConfigAsset))]
    public class LevelConfigAssetEditor : UnityEditor.Editor
    {
        private LevelConfigAsset _asset;

        private void OnEnable()
        {
            _asset = (LevelConfigAsset)target;
            GridSceneOverlay.SetActiveAsset(_asset);
        }

        private void OnDisable()
        {
            GridSceneOverlay.SetActiveAsset(null);
        }

        public override void OnInspectorGUI()
        {
            var levelConfig = _asset.LevelConfig;

            EditorGUI.BeginChangeCheck();
            levelConfig.LevelId = EditorGUILayout.TextField("Level ID", levelConfig.LevelId);
            levelConfig.DisplayName = EditorGUILayout.TextField("Display Name", levelConfig.DisplayName);
            levelConfig.Description = EditorGUILayout.TextField("Description", levelConfig.Description);
            levelConfig.Version = EditorGUILayout.IntField("Version", levelConfig.Version);

            EditorGUILayout.Space(4);
            levelConfig.Width = EditorGUILayout.IntSlider("Width", levelConfig.Width, 1, 500);
            levelConfig.Height = EditorGUILayout.IntSlider("Height", levelConfig.Height, 1, 500);
            levelConfig.CellSize = EditorGUILayout.Slider("Cell Size", levelConfig.CellSize, 0.1f, 10f);
            levelConfig.OriginX = EditorGUILayout.FloatField("Origin X", levelConfig.OriginX);
            levelConfig.OriginY = EditorGUILayout.FloatField("Origin Y", levelConfig.OriginY);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_asset, "Edit Level Config Asset");
                _asset.EnsureCellsArray();
                _asset.Save();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            _asset.EnsureCellsArray();

            // 统计各类型格子数量
            int noneCount = 0, solidCount = 0, walkableCount = 0, placeableCount = 0, compositeCount = 0;
            for (int i = 0; i < levelConfig.Cells.Length; i++)
            {
                var ct = (CellType)levelConfig.Cells[i];
                switch (ct)
                {
                    case CellType.None: noneCount++; break;
                    case CellType.Solid: solidCount++; break;
                    case CellType.Walkable: walkableCount++; break;
                    case CellType.Placeable: placeableCount++; break;
                    case CellType.WalkableAndPlaceable: compositeCount++; break;
                }
            }

            EditorGUILayout.LabelField($"Total Cells: {levelConfig.Cells.Length}");
            EditorGUILayout.LabelField($"Goal Points: {levelConfig.GoalPoints?.Length ?? 0} (在 GridEditor 中编辑)");
            EditorGUILayout.LabelField($"Spawn Points: {levelConfig.SpawnPoints?.Length ?? 0} (在 GridEditor 中编辑)");
            EditorGUILayout.LabelField($"None: {noneCount} | Solid: {solidCount} | Walkable: {walkableCount}");
            EditorGUILayout.LabelField($"Placeable: {placeableCount} | Composite: {compositeCount}");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open Grid Editor", GUILayout.Height(28)))
            {
                var window = EditorWindow.GetWindow<GridEditorWindow>();
                window.Show();
                // GridEditorWindow 会自动使用当前选中的 asset
            }

            if (GUILayout.Button("Show in Scene", GUILayout.Height(28)))
            {
                GridSceneOverlay.SetActiveAsset(_asset);
                GridSceneOverlay.SetEnabled(true);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export to Data", GUILayout.Height(24)))
            {
                var exportConfig = _asset.ToLevelConfig();
                string path = LevelConfigLoader.GetEditorFilePath(_asset.LevelConfig.LevelId);
                LevelConfigLoader.SaveToFile(exportConfig, path);
                AssetDatabase.Refresh();
                Debug.Log($"Exported to: {path}");
            }

            if (GUILayout.Button("Export to Resources", GUILayout.Height(24)))
            {
                var exportConfig = _asset.ToLevelConfig();
                string path = LevelConfigLoader.GetResourcesFilePath(_asset.LevelConfig.LevelId);
                LevelConfigLoader.SaveToFile(exportConfig, path);
                AssetDatabase.Refresh();
                Debug.Log($"Exported to Resources: {path}");
            }

            EditorGUILayout.EndHorizontal();

            // 验证
            var configForValidation = _asset.ToLevelConfig();
            if (!configForValidation.Validate(out var error))
            {
                EditorGUILayout.HelpBox($"Validation Error: {error}", MessageType.Error);
            }
        }
    }
}
#endif

