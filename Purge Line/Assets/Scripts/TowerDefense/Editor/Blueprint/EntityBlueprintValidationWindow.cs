#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.Editor.Blueprint
{
    internal sealed class EntityBlueprintValidationWindow : EditorWindow
    {
        private readonly List<string> _diffLines = new List<string>();

        public static void ShowDiff(IReadOnlyList<string> lines)
        {
            var window = GetWindow<EntityBlueprintValidationWindow>("Blueprint Validation");
            window.minSize = new Vector2(600f, 300f);
            window.SetDiff(lines);
            window.Show();
        }

        private void SetDiff(IReadOnlyList<string> lines)
        {
            _diffLines.Clear();
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                    _diffLines.Add(lines[i]);
            }

            BuildUI();
        }

        private void CreateGUI()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var headerLabel = new Label("Serialization -> Deserialization Validation")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6
                }
            };
            rootVisualElement.Add(headerLabel);

            var status = new Label(_diffLines.Count == 0 ? "No differences found." : $"Differences: {_diffLines.Count}");
            status.style.color = _diffLines.Count == 0 ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
            status.style.marginBottom = 6;
            rootVisualElement.Add(status);

            var list = new ListView(_diffLines, 22f, () => new Label(), (element, index) =>
            {
                if (element is Label label)
                {
                    label.text = _diffLines[index];
                    label.style.color = new Color(0.95f, 0.3f, 0.3f);
                }
            })
            {
                style = { flexGrow = 1f }
            };
            rootVisualElement.Add(list);
        }
    }
}
#endif


