#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    [InitializeOnLoad]
    public static class EntityDataPlayModePrecheck
    {
        private static bool _sIsRunning;

        static EntityDataPlayModePrecheck()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || _sIsRunning)
                return;

            _sIsRunning = true;
            try
            {
                bool ok = EntityDataEditorUtility.RunBlueprintPreflight(false, out string report);
                if (!ok)
                {
                    EditorApplication.isPlaying = false;
                    EditorUtility.DisplayDialog("进入 Play 前检查失败", report, "确定");
                }
                else
                {
                    Debug.Log($"[EntityData] PlayMode preflight passed. {report}");
                }
            }
            catch (Exception ex)
            {
                EditorApplication.isPlaying = false;
                EditorUtility.DisplayDialog("进入 Play 前检查异常", ex.Message, "确定");
            }
            finally
            {
                _sIsRunning = false;
            }
        }
    }

    public sealed class EntityDataBuildPrecheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool ok = EntityDataEditorUtility.RunBlueprintPreflight(false, out string message);
            if (!ok)
                throw new BuildFailedException($"[EntityData] Build preflight failed: {message}");

            Debug.Log($"[EntityData] Build preflight passed. {message}");
        }
    }
}
#endif


