#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TowerDefense.Data.EntityData;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.Editor.EntityData
{
    public sealed class EntityDataHubWindow : EditorWindow
    {
        private readonly Dictionary<EntityType, Foldout> _typeFoldouts = new Dictionary<EntityType, Foldout>();
        private readonly Dictionary<EntityType, string> _createNames = new Dictionary<EntityType, string>();
        private readonly Dictionary<EntityType, Label> _createHints = new Dictionary<EntityType, Label>();
        private TextField _searchField;
        private ScrollView _content;
        private string _searchKeyword = string.Empty;

        [MenuItem("PurgeLine/Entity Data Hub")]
        public static void Open()
        {
            var window = GetWindow<EntityDataHubWindow>("Entity Data Hub");
            window.minSize = new Vector2(980f, 660f);
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            BuildToolbar();
            _content = new ScrollView { style = { flexGrow = 1f } };
            rootVisualElement.Add(_content);
            Refresh();
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginBottom = 6f
                }
            };

            var row1 = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap
                }
            };
            row1.style.marginBottom = 4f;

            var row2 = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            _searchField = new TextField("Search") { value = _searchKeyword };
            _searchField.style.minWidth = 260f;
            _searchField.style.maxWidth = 420f;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchKeyword = evt.newValue ?? string.Empty;
                Refresh();
            });
            row1.Add(_searchField);

            var refreshButton = new Button(Refresh) { text = "刷新" };
            refreshButton.style.minWidth = 88f;
            refreshButton.style.marginLeft = 6f;
            row1.Add(refreshButton);

            var fullValidateButton = new Button(DoFullValidation) { text = "全量校验" };
            fullValidateButton.style.minWidth = 96f;
            fullValidateButton.style.marginLeft = 6f;
            row1.Add(fullValidateButton);

            var expandAllButton = new Button(() => SetAllTypeFoldoutState(true)) { text = "全部展开" };
            expandAllButton.style.minWidth = 88f;
            row2.Add(expandAllButton);

            var collapseAllButton = new Button(() => SetAllTypeFoldoutState(false)) { text = "全部折叠" };
            collapseAllButton.style.minWidth = 88f;
            collapseAllButton.style.marginLeft = 6f;
            row2.Add(collapseAllButton);

            toolbar.Add(row1);
            toolbar.Add(row2);

            rootVisualElement.Add(toolbar);
        }

        private void SetAllTypeFoldoutState(bool expanded)
        {
            foreach (var pair in _typeFoldouts)
                pair.Value.value = expanded;
        }

        private void Refresh()
        {
            var registry = EntityDataEditorUtility.GetOrCreateRegistry();
            _content.Clear();
            _typeFoldouts.Clear();
            _createHints.Clear();

            foreach (EntityType type in Enum.GetValues(typeof(EntityType)))
            {
                if (type == EntityType.Max)
                    continue;

                var foldout = new Foldout { text = type.ToString(), value = true };
                _typeFoldouts[type] = foldout;

                if (!_createNames.ContainsKey(type))
                    _createNames[type] = string.Empty;

                var createPanel = new VisualElement
                {
                    style =
                    {
                        borderTopWidth = 1f,
                        borderBottomWidth = 1f,
                        borderLeftWidth = 1f,
                        borderRightWidth = 1f,
                        borderTopColor = new Color(0.23f, 0.23f, 0.23f),
                        borderBottomColor = new Color(0.23f, 0.23f, 0.23f),
                        borderLeftColor = new Color(0.23f, 0.23f, 0.23f),
                        borderRightColor = new Color(0.23f, 0.23f, 0.23f),
                        borderTopLeftRadius = 4f,
                        borderTopRightRadius = 4f,
                        borderBottomLeftRadius = 4f,
                        borderBottomRightRadius = 4f,
                        paddingLeft = 8f,
                        paddingRight = 8f,
                        paddingTop = 6f,
                        paddingBottom = 6f,
                        marginBottom = 6f
                    }
                };

                createPanel.Add(new Label("新增配置包")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        marginBottom = 4f
                    }
                });

                var createRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        flexWrap = Wrap.Wrap
                    }
                };

                var createNameField = new TextField("新建名称")
                {
                    value = _createNames[type]
                };
                createNameField.style.minWidth = 240f;
                createNameField.style.maxWidth = 340f;
                createNameField.style.flexGrow = 0f;
                createNameField.style.marginRight = 6f;
                createNameField.RegisterValueChangedCallback(evt =>
                {
                    _createNames[type] = evt.newValue ?? string.Empty;
                    UpdateCreateHint(registry, type);
                });
                createRow.Add(createNameField);

                var createButton = new Button(() =>
                {
                    if (!EntityDataEditorUtility.TryValidateCreateName(registry, type, _createNames[type], out var error))
                    {
                        if (_createHints.TryGetValue(type, out var hintLabel))
                            SetHint(hintLabel, error, true);
                        return;
                    }

                    EntityDataEditorUtility.CreateNewRecord(registry, type, _createNames[type]);
                    _createNames[type] = string.Empty;
                    Refresh();
                }) { text = "新增配置包" };
                createButton.style.minWidth = 110f;
                createButton.style.marginRight = 6f;
                createRow.Add(createButton);

                var expandTypeButton = new Button(() => SetTypeRecordFoldouts(type, true)) { text = "展开该分类" };
                expandTypeButton.style.marginRight = 6f;
                createRow.Add(expandTypeButton);

                createRow.Add(new Button(() => SetTypeRecordFoldouts(type, false)) { text = "折叠该分类" });
                createPanel.Add(createRow);

                var createHint = new Label();
                createHint.style.marginTop = 4f;
                _createHints[type] = createHint;
                createPanel.Add(createHint);

                foldout.Add(createPanel);
                UpdateCreateHint(registry, type);

                IEnumerable<EntityConfigRegistryRecord> records = registry.Records
                    .Where(x => x.EntityType == type)
                    .OrderBy(x => x.LocalId);

                if (!string.IsNullOrWhiteSpace(_searchKeyword))
                {
                    records = records.Where(x =>
                        x.EntityIdToken.IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.EntityIdEnumName.IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (x.DisplayName ?? string.Empty).IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                foreach (var record in records)
                    foldout.Add(BuildRecordFoldout(registry, record));

                _content.Add(foldout);
            }
        }

        private static void SetTypeRecordFoldouts(EntityType type, bool expanded)
        {
            var root = Resources.FindObjectsOfTypeAll<EntityDataHubWindow>().FirstOrDefault()?.rootVisualElement;
            if (root == null)
                return;

            var typeFoldouts = root.Query<Foldout>().ToList();
            foreach (var foldout in typeFoldouts)
            {
                if (foldout.text == type.ToString())
                {
                    foreach (var child in foldout.Query<Foldout>().ToList())
                        child.value = expanded;
                    break;
                }
            }
        }

        private VisualElement BuildRecordFoldout(EntityConfigRegistryAsset registry, EntityConfigRegistryRecord record)
        {
            var package = EntityDataEditorUtility.LoadPackageFromRecord(record);
            var entityFoldout = new Foldout
            {
                text = string.IsNullOrWhiteSpace(record.DisplayName) ? record.EntityIdToken : record.DisplayName,
                value = false
            };

            entityFoldout.Add(new Label($"名称: {package.DisplayNameForLog}"));
            entityFoldout.Add(new Label($"枚举: {record.EntityIdEnumName} ({record.LocalId})"));
            entityFoldout.Add(new Label($"Token: {record.EntityIdToken}"));
            entityFoldout.Add(new Label($"Address: {record.Address}"));

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4f } };

            actions.Add(new Button(() =>
            {
                var single = EntityDataEditorUtility.GetOrCreateTypedEditorAsset(record.EntityType);
                EntityDataEditorUtility.LoadIntoTypedEditor(record, single);
                Selection.activeObject = single;
                EditorGUIUtility.PingObject(single);
            }) { text = "编辑" });

            actions.Add(new Button(() =>
            {
                var result = EntityDataEditorUtility.ValidateRecord(registry, record);
                EditorUtility.DisplayDialog("单项校验", result.Message, "确定");
            }) { text = "校验" });

            actions.Add(new Button(() =>
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "删除配置包",
                    $"确定删除 {record.EntityIdToken} 吗？\n会执行引用检查。",
                    "删除",
                    "取消");
                if (!confirm)
                    return;

                if (!EntityDataEditorUtility.TryDeleteRecordWithReferenceCheck(registry, record, out string report))
                {
                    EditorUtility.DisplayDialog("删除被阻止", report, "确定");
                    return;
                }

                Refresh();
            }) { text = "删除" });

            entityFoldout.Add(actions);
            return entityFoldout;
        }

        private static void DoFullValidation()
        {
            var registry = EntityDataEditorUtility.GetOrCreateRegistry();
            var result = EntityDataEditorUtility.ValidateAll(registry);
            EditorUtility.DisplayDialog("全量校验", result.Message, "确定");
        }

        private void UpdateCreateHint(EntityConfigRegistryAsset registry, EntityType type)
        {
            if (!_createHints.TryGetValue(type, out var label))
                return;

            if (EntityDataEditorUtility.TryValidateCreateName(registry, type, _createNames[type], out var error))
            {
                string slug = EntityDataAddressRules.ToSlug(_createNames[type]);
                string enumName = EntityIdEnumGenerator.Sanitize(_createNames[type]);
                SetHint(label, $"address后缀: {slug} | 枚举名: {enumName}", false);
                return;
            }

            bool isEmptyInput = string.IsNullOrWhiteSpace(_createNames[type]);
            SetHint(label, isEmptyInput ? "请输入名称后再新增配置包" : error, isEmptyInput ? false : true);
        }

        private static void SetHint(Label label, string text, bool isError)
        {
            label.text = text;
            label.style.color = isError ? new Color(0.84f, 0.25f, 0.25f) : new Color(0.45f, 0.8f, 0.45f);
        }
    }
}
#endif

