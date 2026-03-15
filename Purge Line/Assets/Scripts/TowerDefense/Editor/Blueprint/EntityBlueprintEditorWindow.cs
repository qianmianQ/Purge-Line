#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Data.Blueprint;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.Editor.Blueprint
{
    public sealed class EntityBlueprintEditorWindow : EditorWindow
    {
        private const string DefaultExtension = "bytes";
        private const string LastDirectoryKey = "TowerDefense.EntityBlueprint.LastDirectory";
        private const double AutoSaveIntervalSeconds = 20.0;

        private EntityBlueprintEditorState _state;
        private EntityBlueprintComponentCatalog _catalog;
        private readonly List<string> _consoleLines = new List<string>();
        private readonly Dictionary<Type, FieldInfo[]> _componentFieldCache = new Dictionary<Type, FieldInfo[]>();

        private TextField _searchField;
        private ListView _componentListView;
        private VisualElement _libraryRoot;
        private VisualElement _recentFilesRoot;
        private Label _statusLabel;
        private Label _metaStatusLabel;
        private ListView _consoleList;
        private TwoPaneSplitView _splitView;
        private TextField _blueprintNameField;

        private double _lastAutoSaveAt;
        private string _searchKeyword = string.Empty;
        private bool _initialized;

        [MenuItem("PurgeLine/Entity Blueprint Editor")]
        public static void Open()
        {
            var window = GetWindow<EntityBlueprintEditorWindow>("Entity Blueprint");
            window.minSize = new Vector2(980f, 640f);
            window.Show();
        }

        public static void OpenAndLoad(string absolutePath)
        {
            var window = GetWindow<EntityBlueprintEditorWindow>("Entity Blueprint");
            window.minSize = new Vector2(980f, 640f);
            window.Show();
            EditorApplication.delayCall += () =>
            {
                if (window != null)
                    window.OpenFile(absolutePath);
            };
        }

        private void OnEnable()
        {
            EnsureStateInitialized();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateGUI()
        {
            EnsureStateInitialized();

            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Scripts/TowerDefense/Editor/Blueprint/EntityBlueprintEditor.uss");
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            BuildToolbar();
            BuildRecentFilesBar();
            BuildMainLayout();
            BuildBottomArea();

            RefreshLibrary();
            RefreshComponentCards();
            RefreshStatus();
            ApplyLayoutLock();
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();

            toolbar.Add(CreateToolbarButton("New", () =>
            {
                _state.NewDocument("NewEntityBlueprint");
                RefreshComponentCards();
                RefreshStatus();
                Log("New blueprint created.");
            }));

            toolbar.Add(CreateToolbarButton("Open", OpenFile));
            toolbar.Add(CreateToolbarButton("Save", SaveFile));
            _blueprintNameField = new TextField("Blueprint Name")
            {
                value = _state.CurrentDocument.blueprintName
            };
            _blueprintNameField.style.minWidth = 240f;
            toolbar.Add(_blueprintNameField);
            toolbar.Add(CreateToolbarButton("Apply Name", RenameBlueprint));
            toolbar.Add(CreateToolbarButton("Create Instance", CreateInstance));
            toolbar.Add(CreateToolbarButton("Run Test", RunQuickTest));
            toolbar.Add(CreateToolbarButton("Deserialize Test", TestDeserialize));
            toolbar.Add(CreateToolbarButton("Validate Data", ValidateRoundTrip));

            var lockToggle = new ToolbarToggle { text = "Lock Layout", value = _state.LayoutLocked };
            lockToggle.RegisterValueChangedCallback(_ =>
            {
                _state.ToggleLayoutLock();
                ApplyLayoutLock();
                Log(_state.LayoutLocked ? "Layout locked." : "Layout unlocked.");
            });
            toolbar.Add(lockToggle);

            rootVisualElement.Add(toolbar);
        }

        private void BuildRecentFilesBar()
        {
            _recentFilesRoot = new VisualElement();
            _recentFilesRoot.AddToClassList("recent-row");
            rootVisualElement.Add(_recentFilesRoot);
            RefreshRecentFilesBar();
        }

        private void RefreshRecentFilesBar()
        {
            if (_recentFilesRoot == null || _state == null)
                return;

            _recentFilesRoot.Clear();
            _recentFilesRoot.Add(new Label("Recent:"));

            for (int i = 0; i < _state.RecentFiles.Count; i++)
            {
                string path = _state.RecentFiles[i];
                string shortName = Path.GetFileName(path);
                var button = new Button(() => OpenFile(path)) { text = shortName };
                button.tooltip = path;
                _recentFilesRoot.Add(button);
            }
        }

        private void BuildMainLayout()
        {
            _splitView = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            _splitView.style.flexGrow = 1f;

            var leftPane = new VisualElement();
            leftPane.AddToClassList("left-pane");
            _splitView.Add(leftPane);

            var rightPane = new VisualElement();
            rightPane.AddToClassList("right-pane");
            _splitView.Add(rightPane);

            BuildLibraryPane(leftPane);
            BuildComponentPane(rightPane);

            rootVisualElement.Add(_splitView);
        }

        private void BuildLibraryPane(VisualElement root)
        {
            var paneTitle = new Label("Component Library");
            paneTitle.AddToClassList("pane-title");
            root.Add(paneTitle);

            _searchField = new TextField("Search") { value = _searchKeyword };
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchKeyword = evt.newValue;
                RefreshLibrary();
            });
            root.Add(_searchField);

            var libraryActions = new VisualElement();
            libraryActions.style.flexDirection = FlexDirection.Row;
            libraryActions.Add(new Button(() => SetLibraryFoldouts(true)) { text = "Expand All" });
            libraryActions.Add(new Button(() => SetLibraryFoldouts(false)) { text = "Collapse All" });
            root.Add(libraryActions);

            _libraryRoot = new ScrollView();
            _libraryRoot.style.flexGrow = 1f;
            root.Add(_libraryRoot);
        }

        private void BuildComponentPane(VisualElement root)
        {
            var paneTitle = new Label("Entity Components");
            paneTitle.AddToClassList("pane-title");
            root.Add(paneTitle);

            _componentListView = new ListView
            {
                showFoldoutHeader = false,
                reorderable = !_state.LayoutLocked,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                style = { flexGrow = 1f }
            };
            _componentListView.makeItem = MakeComponentItem;
            _componentListView.bindItem = BindComponentItem;
            _componentListView.itemsSource = _state.CurrentDocument.components;
            _componentListView.itemsAdded += _ => MarkDirtyAndRefreshStatus();
            _componentListView.itemsRemoved += _ => MarkDirtyAndRefreshStatus();
            _componentListView.itemIndexChanged += (_, _) => MarkDirtyAndRefreshStatus();
            root.Add(_componentListView);
        }

        private VisualElement MakeComponentItem()
        {
            return new VisualElement();
        }

        private void BindComponentItem(VisualElement element, int index)
        {
            element.Clear();
            if (index < 0 || index >= _state.CurrentDocument.components.Count)
                return;

            ComponentRecord componentRecord = _state.CurrentDocument.components[index];
            string displayName = Type.GetType(componentRecord.componentTypeName)?.Name ?? componentRecord.componentTypeName;

            var card = new VisualElement();
            card.AddToClassList("component-card");

            var header = new VisualElement();
            header.AddToClassList("component-header");

            var foldout = new Foldout
            {
                text = displayName,
                value = componentRecord.expanded
            };
            foldout.style.flexGrow = 1f;
            foldout.RegisterValueChangedCallback(evt => componentRecord.expanded = evt.newValue);

            var removeButton = new Button(() =>
            {
                _state.CurrentDocument.components.RemoveAt(index);
                MarkDirtyAndRefreshStatus();
                RefreshComponentCards();
                Log($"Removed component: {displayName}");
            })
            {
                text = "Remove"
            };

            header.Add(foldout);
            header.Add(removeButton);
            card.Add(header);

            Type componentType = Type.GetType(componentRecord.componentTypeName);
            if (componentType == null)
            {
                card.Add(new HelpBox("Component type not found in current domain.", HelpBoxMessageType.Warning));
            }
            else
            {
                var fieldsContainer = new VisualElement();
                fieldsContainer.AddToClassList("component-fields");

                for (int i = 0; i < componentRecord.fields.Count; i++)
                {
                    FieldRecord field = componentRecord.fields[i];
                    var fieldUi = EntityBlueprintFieldDrawer.BuildField(field, MarkDirtyAndRefreshStatus);
                    fieldsContainer.Add(fieldUi);
                }

                foldout.Add(fieldsContainer);
            }

            element.Add(card);
        }

        private void BuildBottomArea()
        {
            var bottom = new VisualElement();
            bottom.AddToClassList("bottom-root");

            var statusBar = new VisualElement();
            statusBar.AddToClassList("status-bar");
            _statusLabel = new Label();
            _metaStatusLabel = new Label();
            statusBar.Add(_statusLabel);
            statusBar.Add(_metaStatusLabel);
            bottom.Add(statusBar);

            _consoleList = new ListView(_consoleLines, 20f, () => new Label(), (element, i) =>
            {
                if (element is Label line)
                    line.text = _consoleLines[i];
            })
            {
                style = { flexGrow = 1f, height = 120f }
            };
            _consoleList.AddToClassList("console-list");
            bottom.Add(_consoleList);

            rootVisualElement.Add(bottom);
        }

        private Button CreateToolbarButton(string text, Action action)
        {
            return new Button(action) { text = text };
        }

        private void RefreshLibrary()
        {
            if (_libraryRoot == null || _catalog == null)
                return;

            _libraryRoot.Clear();

            var favoritesSection = new Foldout { text = "Favorites", value = true };
            IReadOnlyList<ComponentTypeInfo> favorites = _catalog.Favorites();
            if (favorites.Count == 0)
            {
                favoritesSection.Add(new Label("No favorites"));
            }
            else
            {
                for (int i = 0; i < favorites.Count; i++)
                    favoritesSection.Add(BuildComponentLibraryRow(favorites[i]));
            }
            _libraryRoot.Add(favoritesSection);

            IReadOnlyList<ComponentTypeInfo> source = _catalog.Search(_searchKeyword);
            var grouped = source.GroupBy(x => x.Category).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var categoryFoldout = new Foldout { text = group.Key, value = false };
                foreach (ComponentTypeInfo component in group.OrderBy(x => x.DisplayName))
                    categoryFoldout.Add(BuildComponentLibraryRow(component));
                _libraryRoot.Add(categoryFoldout);
            }
        }

        private void SetLibraryFoldouts(bool expanded)
        {
            if (_libraryRoot == null)
                return;

            foreach (var foldout in _libraryRoot.Query<Foldout>().ToList())
            {
                foldout.value = expanded;
            }
        }

        private VisualElement BuildComponentLibraryRow(ComponentTypeInfo component)
        {
            var row = new VisualElement();
            row.AddToClassList("library-row");

            var addButton = new Button(() => AddComponent(component)) { text = component.DisplayName };
            addButton.style.flexGrow = 1f;

            var favoriteButton = new Button(() =>
            {
                _catalog.ToggleFavorite(component);
                RefreshLibrary();
            })
            {
                text = component.IsFavorite ? "*" : "+"
            };
            favoriteButton.style.width = 28f;

            row.Add(addButton);
            row.Add(favoriteButton);
            return row;
        }

        private void AddComponent(ComponentTypeInfo typeInfo)
        {
            if (typeInfo == null)
                return;

            bool exists = _state.CurrentDocument.components.Any(c =>
                string.Equals(c.componentTypeName, typeInfo.AssemblyQualifiedName, StringComparison.Ordinal));
            if (exists)
            {
                Log($"Component already added: {typeInfo.DisplayName}");
                return;
            }

            var record = new ComponentRecord
            {
                componentTypeName = typeInfo.AssemblyQualifiedName,
                category = typeInfo.Category,
                expanded = true
            };

            FieldInfo[] fields = GetSupportedFields(typeInfo.Type);
            for (int i = 0; i < fields.Length; i++)
            {
                object defaultValue = fields[i].FieldType.IsValueType ? Activator.CreateInstance(fields[i].FieldType) : string.Empty;
                record.fields.Add(new FieldRecord
                {
                    fieldPath = fields[i].Name,
                    fieldTypeName = fields[i].FieldType.AssemblyQualifiedName,
                    serializedValue = EntityBlueprintTypeUtility.SerializeValue(fields[i].FieldType, defaultValue)
                });
            }

            _state.CurrentDocument.components.Add(record);
            MarkDirtyAndRefreshStatus();
            RefreshComponentCards();
            Log($"Added component: {typeInfo.DisplayName}");
        }

        private FieldInfo[] GetSupportedFields(Type type)
        {
            if (_componentFieldCache.TryGetValue(type, out FieldInfo[] cached))
                return cached;

            FieldInfo[] all = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var supported = new List<FieldInfo>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (EntityBlueprintTypeUtility.IsSupportedFieldType(all[i].FieldType))
                    supported.Add(all[i]);
            }

            cached = supported.ToArray();
            _componentFieldCache[type] = cached;
            return cached;
        }

        private void RefreshComponentCards()
        {
            if (_componentListView == null || _state == null)
                return;

            _componentListView.itemsSource = _state.CurrentDocument.components;
            _componentListView.reorderable = !_state.LayoutLocked;
            _componentListView.Rebuild();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null || _metaStatusLabel == null || _state == null)
                return;

            string path = string.IsNullOrEmpty(_state.CurrentFilePath) ? "(unsaved)" : _state.CurrentFilePath;
            string saveState = _state.IsDirty ? "Unsaved" : "Saved";
            _statusLabel.text = $"File: {path}";
            _metaStatusLabel.text = $"State: {saveState} | Components: {_state.CurrentDocument.components.Count}";
            if (_blueprintNameField != null)
                _blueprintNameField.value = _state.CurrentDocument.blueprintName;
        }

        private void OpenFile()
        {
            string startDir = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
            string path = EditorUtility.OpenFilePanel("Open Entity Blueprint", startDir, DefaultExtension);
            OpenFile(path);
        }

        private void OpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                var document = EntityBlueprintBinarySerializer.Load(path);
                _state.ReplaceDocument(document, path);
                EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? Application.dataPath);

                RefreshRecentFilesBar();
                RefreshComponentCards();
                RefreshStatus();
                Log($"Opened: {path}");
            }
            catch (Exception ex)
            {
                Log($"Open failed: {ex.Message}");
            }
        }

        private void SaveFile()
        {
            string path = _state.CurrentFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                string startDir = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
                path = EditorUtility.SaveFilePanel("Save Entity Blueprint", startDir, _state.CurrentDocument.blueprintName, DefaultExtension);
                if (string.IsNullOrWhiteSpace(path))
                    return;
            }

            SaveFile(path);
        }

        private void SaveFile(string path)
        {
            try
            {
                EntityBlueprintBinarySerializer.Save(path, _state.CurrentDocument);
                _state.MarkSaved(path);
                EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? Application.dataPath);

                RefreshRecentFilesBar();
                RefreshStatus();
                Log($"Saved: {path}");
            }
            catch (Exception ex)
            {
                Log($"Save failed: {ex.Message}");
            }
        }

        private void RenameBlueprint()
        {
            string newName = _blueprintNameField?.value?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                Log("Rename failed: blueprint name is empty.");
                return;
            }

            string currentName = _state.CurrentDocument.blueprintName;
            if (string.Equals(currentName, newName, StringComparison.Ordinal))
            {
                Log("Blueprint name unchanged.");
                return;
            }

            _state.CurrentDocument.blueprintName = newName;

            if (!string.IsNullOrWhiteSpace(_state.CurrentFilePath))
            {
                string dir = Path.GetDirectoryName(_state.CurrentFilePath);
                string newPath = Path.Combine(dir ?? string.Empty, $"{newName}.{DefaultExtension}");
                SaveFile(newPath);
            }
            else
            {
                MarkDirtyAndRefreshStatus();
            }

            Log($"Blueprint renamed to {newName}");
        }

        private void CreateInstance()
        {
            if (Application.isPlaying)
            {
                try
                {
                    var world = World.DefaultGameObjectInjectionWorld;
                    if (world == null)
                    {
                        Log("Create failed: default world is null.");
                        return;
                    }

                    Entity entity = EntityBlueprintRuntimeFactory.Create(world.EntityManager, _state.CurrentDocument, float3.zero);
                    Log($"Entity instantiated in current world: {entity.Index}:{entity.Version}");
                }
                catch (Exception ex)
                {
                    Log($"Create failed: {ex.Message}");
                }

                return;
            }

            if (string.IsNullOrEmpty(_state.CurrentFilePath))
            {
                Log("Create to SubScene requires saving blueprint first.");
                return;
            }

            var go = new GameObject($"BP_{_state.CurrentDocument.blueprintName}");
            var authoring = go.AddComponent<EntityBlueprintAuthoring>();
            authoring.BlueprintPath = _state.CurrentFilePath;
            Undo.RegisterCreatedObjectUndo(go, "Create Blueprint Authoring");
            Selection.activeGameObject = go;
            Log("Authoring object created for SubScene baking.");
        }

        private void RunQuickTest()
        {
            if (_state.IsDirty || string.IsNullOrEmpty(_state.CurrentFilePath))
                SaveFile();

            if (string.IsNullOrEmpty(_state.CurrentFilePath))
            {
                Log("Run test canceled: blueprint has no file path.");
                return;
            }

            SessionState.SetString("TowerDefense.EntityBlueprint.RunTestPath", _state.CurrentFilePath);
            Log("Run test: save completed, entering play mode...");
            EditorApplication.EnterPlaymode();
        }

        private void ValidateRoundTrip()
        {
            try
            {
                EntityBlueprintDocument source = _state.CurrentDocument;
                EntityBlueprintDocument loaded = EntityBlueprintBinarySerializer.RoundTrip(source);
                IReadOnlyList<string> diff = BuildDiff(source, loaded);
                EntityBlueprintValidationWindow.ShowDiff(diff);
                Log(diff.Count == 0
                    ? "Validation passed: no differences detected."
                    : $"Validation completed with {diff.Count} differences.");
            }
            catch (Exception ex)
            {
                Log($"Validation failed: {ex.Message}");
            }
        }

        private void TestDeserialize()
        {
            try
            {
                EntityBlueprintDocument loaded = EntityBlueprintBinarySerializer.RoundTrip(_state.CurrentDocument);
                if (loaded != null)
                    Log($"Deserialize test passed: {loaded.components.Count} components loaded.");
                else
                    Log("Deserialize test failed: returned null document.");
            }
            catch (Exception ex)
            {
                Log($"Deserialize test failed: {ex.Message}");
            }
        }

        private IReadOnlyList<string> BuildDiff(EntityBlueprintDocument source, EntityBlueprintDocument loaded)
        {
            var lines = new List<string>();
            if (source.components.Count != loaded.components.Count)
                lines.Add($"Component count mismatch: source={source.components.Count}, loaded={loaded.components.Count}");

            int componentCount = Math.Min(source.components.Count, loaded.components.Count);
            for (int i = 0; i < componentCount; i++)
            {
                ComponentRecord a = source.components[i];
                ComponentRecord b = loaded.components[i];
                if (!string.Equals(a.componentTypeName, b.componentTypeName, StringComparison.Ordinal))
                    lines.Add($"[{i}] Component type mismatch: {a.componentTypeName} != {b.componentTypeName}");

                if (a.fields.Count != b.fields.Count)
                    lines.Add($"[{i}] Field count mismatch: {a.fields.Count} != {b.fields.Count}");

                int fieldCount = Math.Min(a.fields.Count, b.fields.Count);
                for (int f = 0; f < fieldCount; f++)
                {
                    FieldRecord fa = a.fields[f];
                    FieldRecord fb = b.fields[f];
                    if (!string.Equals(fa.fieldPath, fb.fieldPath, StringComparison.Ordinal))
                        lines.Add($"[{i}:{f}] Field path mismatch: {fa.fieldPath} != {fb.fieldPath}");
                    if (!string.Equals(fa.serializedValue, fb.serializedValue, StringComparison.Ordinal))
                        lines.Add($"[{i}:{f}] Value mismatch {fa.fieldPath}: {fa.serializedValue} != {fb.serializedValue}");
                }
            }

            return lines;
        }

        private void ApplyLayoutLock()
        {
            if (_componentListView == null || _state == null || _splitView == null)
                return;

            _componentListView.reorderable = !_state.LayoutLocked;

            var dragLine = _splitView.Q(className: "unity-two-pane-split-view__dragline");
            if (dragLine != null)
            {
                dragLine.pickingMode = _state.LayoutLocked ? PickingMode.Ignore : PickingMode.Position;
                dragLine.style.opacity = _state.LayoutLocked ? 0.3f : 1f;
            }
        }

        private void MarkDirtyAndRefreshStatus()
        {
            _state.MarkDirty();
            RefreshStatus();
        }

        private void OnEditorUpdate()
        {
            if (!_initialized || _state == null)
                return;

            if (_state.IsDirty && !string.IsNullOrEmpty(_state.CurrentFilePath))
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - _lastAutoSaveAt >= AutoSaveIntervalSeconds)
                {
                    _lastAutoSaveAt = now;
                    SaveFile(_state.CurrentFilePath);
                    Log("Auto-saved current blueprint.");
                }
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            string path = SessionState.GetString("TowerDefense.EntityBlueprint.RunTestPath", string.Empty);
            if (string.IsNullOrEmpty(path))
                return;

            SessionState.EraseString("TowerDefense.EntityBlueprint.RunTestPath");
            if (!File.Exists(path))
                return;

            try
            {
                EntityBlueprintDocument doc = EntityBlueprintBinarySerializer.Load(path);
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                    return;

                Entity entity = EntityBlueprintRuntimeFactory.Create(world.EntityManager, doc, float3.zero);

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 10f);
                }

                Log($"Run test entity created: {entity.Index}:{entity.Version}");
            }
            catch (Exception ex)
            {
                Log($"Run test failed: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _consoleLines.Add(line);
            if (_consoleLines.Count > 200)
                _consoleLines.RemoveAt(0);

            _consoleList?.Rebuild();
        }

        private void EnsureStateInitialized()
        {
            if (_initialized)
                return;

            _state = new EntityBlueprintEditorState();
            _state.InitializeFromEditorPrefs();

            _catalog = new EntityBlueprintComponentCatalog();
            _catalog.InitializeFromEditorPrefs();

            _initialized = true;
        }
    }
}
#endif



