// ============================================================================
// PurgeLine.Resource.Editor — StressTestTool.cs
// 压力测试工具：一键模拟资源频繁加载释放，自动检测内存泄漏
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PurgeLine.Resource.Internal;
using UnityDependencyInjection;
using Object = UnityEngine.Object;

namespace PurgeLine.Resource.Editor
{
    /// <summary>
    /// 资源系统压力测试工具。
    /// 菜单路径: PurgeLine → Resource Stress Test
    /// </summary>
    public sealed class StressTestWindow : EditorWindow
    {
        private string _testAddress = "";
        private int _iterations = 100;
        private int _concurrency = 5;
        private bool _isRunning;
        private string _resultLog = "";
        private Vector2 _logScroll;

        [MenuItem("PurgeLine/Resource Stress Test")]
        public static void ShowWindow()
        {
            var window = GetWindow<StressTestWindow>("Resource Stress Test");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Resource System Stress Test", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _testAddress = EditorGUILayout.TextField("Test Address", _testAddress);
            _iterations = EditorGUILayout.IntSlider("Iterations", _iterations, 10, 10000);
            _concurrency = EditorGUILayout.IntSlider("Concurrency", _concurrency, 1, 20);

            EditorGUILayout.Space(8);

            GUI.enabled = Application.isPlaying && !_isRunning && !string.IsNullOrEmpty(_testAddress);

            if (GUILayout.Button(_isRunning ? "Running..." : "Run Load/Release Stress Test", GUILayout.Height(30)))
            {
                RunLoadReleaseTest().Forget();
            }

            if (GUILayout.Button(_isRunning ? "Running..." : "Run Leak Detection Test", GUILayout.Height(30)))
            {
                RunLeakDetectionTest().Forget();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Results:", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_resultLog, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private async UniTaskVoid RunLoadReleaseTest()
        {
            _isRunning = true;
            _resultLog = $"Starting load/release test: {_iterations} iterations, concurrency={_concurrency}\n";
            Repaint();

            var mgr = GetManager();
            if (mgr == null)
            {
                _resultLog += "ERROR: ResourceManager not available.\n";
                _isRunning = false;
                Repaint();
                return;
            }

            int successCount = 0;
            int failCount = 0;
            float startTime = Time.realtimeSinceStartup;

            int refBefore = mgr.GetRefCount(_testAddress);
            _resultLog += $"Ref count before: {refBefore}\n";

            for (int batch = 0; batch < _iterations; batch += _concurrency)
            {
                var tasks = new List<UniTask>();
                int batchSize = Mathf.Min(_concurrency, _iterations - batch);

                for (int j = 0; j < batchSize; j++)
                {
                    tasks.Add(LoadAndReleaseSingle(mgr, batch + j));
                }

                await UniTask.WhenAll(tasks);

                // 更新计数
                successCount += batchSize;
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            int refAfter = mgr.GetRefCount(_testAddress);

            _resultLog += $"\n--- Results ---\n";
            _resultLog += $"Completed: {successCount}/{_iterations}\n";
            _resultLog += $"Failed: {failCount}\n";
            _resultLog += $"Elapsed: {elapsed:F2}s\n";
            _resultLog += $"Ref count after: {refAfter}\n";
            _resultLog += refAfter == refBefore
                ? "✅ PASS: No reference leak detected.\n"
                : $"❌ FAIL: Reference leak detected! Before={refBefore}, After={refAfter}\n";

            _isRunning = false;
            Repaint();
        }

        private async UniTask LoadAndReleaseSingle(ResourceManager mgr, int index)
        {
            try
            {
                var handle = await mgr.LoadAsync<Object>(_testAddress);
                if (handle.IsValid)
                {
                    mgr.Release(handle);
                }
            }
            catch (System.Exception ex)
            {
                _resultLog += $"  [!] Iteration {index} failed: {ex.Message}\n";
            }
        }

        private async UniTaskVoid RunLeakDetectionTest()
        {
            _isRunning = true;
            _resultLog = "Starting leak detection test...\n";
            Repaint();

            var mgr = GetManager();
            if (mgr == null)
            {
                _resultLog += "ERROR: ResourceManager not available.\n";
                _isRunning = false;
                Repaint();
                return;
            }

            // 加载但不释放
            var handles = new List<ResourceHandle<Object>>();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var h = await mgr.LoadAsync<Object>(_testAddress);
                    handles.Add(h);
                }
                catch (System.Exception ex)
                {
                    _resultLog += $"  Load #{i} failed: {ex.Message}\n";
                }
            }

            int refCountHeld = mgr.GetRefCount(_testAddress);
            _resultLog += $"Loaded 10 times without release. RefCount = {refCountHeld}\n";
            _resultLog += refCountHeld == 10
                ? "✅ PASS: Ref count matches load count.\n"
                : $"⚠️ Unexpected: RefCount={refCountHeld} (expected 10)\n";

            // 释放所有
            foreach (var h in handles)
                mgr.Release(h);

            int refCountAfter = mgr.GetRefCount(_testAddress);
            _resultLog += $"After releasing all: RefCount = {refCountAfter}\n";
            _resultLog += refCountAfter == 0
                ? "✅ PASS: All references released.\n"
                : $"❌ FAIL: Leak detected! RefCount={refCountAfter}\n";

            _isRunning = false;
            Repaint();
        }

        private static ResourceManager GetManager()
        {
            if (!Application.isPlaying || DependencyManager.Instance == null) return null;
            return DependencyManager.Instance.Get<ResourceManager>();
        }
    }
}
#endif

