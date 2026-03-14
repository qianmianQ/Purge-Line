#if UNITY_EDITOR
using System;
using TowerDefense.Bridge;
using TowerDefense.ECS.Lifecycle;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    public sealed class EcsLifecycleWindow : EditorWindow
    {
        private Vector2 _snapshotScroll;
        private string _levelId = "level_01";

        [MenuItem("PurgeLine/ECS Lifecycle Monitor")]
        public static void ShowWindow()
        {
            var window = GetWindow<EcsLifecycleWindow>("ECS Lifecycle");
            window.minSize = new Vector2(620f, 380f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawRuntimeSummary();
            EditorGUILayout.Space(4f);
            DrawSnapshots();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _levelId = EditorGUILayout.TextField("Level Id", _levelId);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后可控制 ECS World。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (!TryResolveServices(out var lifecycle, out var gridBridge, out var framework))
            {
                EditorGUILayout.HelpBox("GameLifetimeScope 或服务不可用。", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start World", GUILayout.Height(24f)))
            {
                lifecycle.StartWorld();
            }

            if (GUILayout.Button("Pause", GUILayout.Height(24f)))
            {
                lifecycle.PauseWorld();
            }

            if (GUILayout.Button("Resume", GUILayout.Height(24f)))
            {
                lifecycle.ResumeWorld();
            }

            if (GUILayout.Button("Stop World", GUILayout.Height(24f)))
            {
                lifecycle.StopWorld();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Session + Load Level", GUILayout.Height(24f)))
            {
                framework.StartGameSession(_levelId);
            }

            if (GUILayout.Button("Load Level Only", GUILayout.Height(24f)))
            {
                gridBridge.LoadLevel(_levelId);
            }

            if (GUILayout.Button("Capture Snapshot", GUILayout.Height(24f)))
            {
                lifecycle.CaptureSnapshot("editor capture");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeSummary()
        {
            if (!Application.isPlaying || !TryResolveServices(out var lifecycle, out _, out _))
                return;

            var stats = lifecycle.RuntimeStatistics;
            EditorGUILayout.BeginHorizontal();
            DrawStatBox("State", lifecycle.State.ToString(), 110f);
            DrawStatBox("Systems", stats.RegisteredSystemCount.ToString(), 70f);
            DrawStatBox("Entities", stats.LastEntityCount.ToString(), 70f);
            DrawStatBox("Frames", stats.Frames.ToString(), 70f);
            DrawStatBox("Avg ms", stats.AvgFrameMs.ToString("F2"), 70f);
            DrawStatBox("Init ms", stats.LastFrameTiming.InitializationMs.ToString("F2"), 70f);
            DrawStatBox("Sim ms", stats.LastFrameTiming.SimulationMs.ToString("F2"), 70f);
            DrawStatBox("Pres ms", stats.LastFrameTiming.PresentationMs.ToString("F2"), 70f);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(stats.LastError))
            {
                EditorGUILayout.HelpBox(stats.LastError, MessageType.Error);
            }
        }

        private void DrawSnapshots()
        {
            if (!Application.isPlaying || !TryResolveServices(out var lifecycle, out _, out _))
                return;

            var snapshots = lifecycle.GetSnapshots();
            EditorGUILayout.LabelField("Snapshots", EditorStyles.boldLabel);

            _snapshotScroll = EditorGUILayout.BeginScrollView(_snapshotScroll);
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                var snap = snapshots[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"#{snap.Sequence} | {snap.TimestampUtc:HH:mm:ss.fff} UTC | {snap.State} | {snap.WorldName}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"entities={snap.EntityCount}, systems={snap.RegisteredSystemCount}, mem={snap.ManagedMemoryBytes / (1024f * 1024f):F2} MB");
                EditorGUILayout.LabelField(
                    $"frame(init/sim/pres)={snap.LastFrameTiming.InitializationMs:F2}/{snap.LastFrameTiming.SimulationMs:F2}/{snap.LastFrameTiming.PresentationMs:F2} ms");
                if (!string.IsNullOrEmpty(snap.Note))
                {
                    EditorGUILayout.LabelField($"note={snap.Note}");
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private static bool TryResolveServices(
            out IEcsLifecycleService lifecycle,
            out IGridBridgeSystem gridBridge,
            out GameFramework framework)
        {
            lifecycle = null;
            gridBridge = null;
            framework = null;

            framework = UnityEngine.Object.FindObjectOfType<GameFramework>();
            if (framework == null)
                return false;

            try
            {
                if (!framework.TryGetEcsLifecycleService(out lifecycle))
                    return false;
                if (!framework.TryGetGridBridgeSystem(out gridBridge))
                    return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DrawStatBox(string label, string value, float width)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(width));
            GUILayout.Label(value, EditorStyles.boldLabel);
            GUILayout.Label(label, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
#endif




