#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TowerDefense.Data.Blueprint;
using UnityEditor;

namespace TowerDefense.Editor.Blueprint
{
    internal sealed class EntityBlueprintEditorState
    {
        private const string RecentFilesKey = "TowerDefense.EntityBlueprint.RecentFiles";
        private const string LayoutLockedKey = "TowerDefense.EntityBlueprint.LayoutLocked";
        private const int MaxRecentFiles = 5;

        private readonly List<string> _recentFiles = new List<string>();

        public EntityBlueprintDocument CurrentDocument { get; private set; }
        public string CurrentFilePath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool LayoutLocked { get; private set; }
        public DateTime LastSaveUtc { get; private set; }

        public IReadOnlyList<string> RecentFiles => _recentFiles;

        public EntityBlueprintEditorState()
        {
            CurrentDocument = new EntityBlueprintDocument();
            LayoutLocked = false;
        }

        public void InitializeFromEditorPrefs()
        {
            LoadRecentFiles();
            LayoutLocked = EditorPrefs.GetBool(LayoutLockedKey, false);
        }

        public void NewDocument(string name)
        {
            CurrentDocument = new EntityBlueprintDocument
            {
                blueprintName = string.IsNullOrWhiteSpace(name) ? "NewEntityBlueprint" : name
            };
            CurrentFilePath = null;
            MarkDirty();
        }

        public void ReplaceDocument(EntityBlueprintDocument document, string filePath)
        {
            CurrentDocument = document ?? new EntityBlueprintDocument();
            CurrentFilePath = filePath;
            IsDirty = false;
            if (!string.IsNullOrEmpty(filePath))
                PushRecent(filePath);
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void MarkSaved(string filePath)
        {
            CurrentFilePath = filePath;
            IsDirty = false;
            LastSaveUtc = DateTime.UtcNow;
            PushRecent(filePath);
        }

        public void ToggleLayoutLock()
        {
            LayoutLocked = !LayoutLocked;
            EditorPrefs.SetBool(LayoutLockedKey, LayoutLocked);
        }

        private void PushRecent(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            _recentFiles.RemoveAll(path => string.Equals(path, filePath, StringComparison.Ordinal));
            _recentFiles.Insert(0, filePath);
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);

            SaveRecentFiles();
        }

        private void LoadRecentFiles()
        {
            _recentFiles.Clear();
            string raw = EditorPrefs.GetString(RecentFilesKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    _recentFiles.Add(parts[i]);
            }
        }

        private void SaveRecentFiles()
        {
            EditorPrefs.SetString(RecentFilesKey, string.Join("|", _recentFiles));
        }
    }
}
#endif

