#if UNITY_EDITOR
using TowerDefense.Data.EntityData;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    public abstract class EntityConfigEditorInspectorBase : UnityEditor.Editor
    {
        private const double AutoSaveIntervalSeconds = 1.0d;

        protected Sprite IconSprite;
        protected Sprite PreviewSprite;
        protected AudioClip SfxClip;

        private double _lastAutoSaveAt;

        protected abstract EntityConfigEditorAssetBase MetaAsset { get; }
        protected abstract IEntityConfigPackage Package { get; }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            DrawEditorHeader();
            DrawTypeSpecificFields();
            DrawCommonFields();
            DrawBlueprintSection();
            DrawActionButtons();

            if (EditorGUI.EndChangeCheck())
            {
                MaybeAutoSave();
            }
        }

        protected abstract void DrawEditorHeader();
        protected abstract void DrawTypeSpecificFields();
        protected abstract void SaveNow(string reason, bool verboseLog);
        protected abstract void LoadNow();

        protected void DrawSharedUiFields(string displayName, string description, string iconAddress, string previewAddress,
            out string outDisplayName, out string outDescription, out string outIconAddress, out string outPreviewAddress)
        {
            outDisplayName = EditorGUILayout.TextField("DisplayName", displayName ?? string.Empty);
            outDescription = EditorGUILayout.TextField("Description", description ?? string.Empty);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("IconAddress", iconAddress ?? string.Empty);
            IconSprite = (Sprite)EditorGUILayout.ObjectField("Icon Sprite", IconSprite, typeof(Sprite), false);
            outIconAddress = IconSprite != null ? EntityDataEditorUtility.EnsureAddressableAddressForSprite(IconSprite) : (iconAddress ?? string.Empty);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("PreviewAddress", previewAddress ?? string.Empty);
            PreviewSprite = (Sprite)EditorGUILayout.ObjectField("Preview Sprite", PreviewSprite, typeof(Sprite), false);
            outPreviewAddress = PreviewSprite != null ? EntityDataEditorUtility.EnsureAddressableAddressForSprite(PreviewSprite) : (previewAddress ?? string.Empty);
        }

        private void DrawCommonFields()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("EntityToken", Package.EntityIdToken ?? string.Empty);
                EditorGUILayout.TextField("Blueprint Address", Package.EntityBlueprintAddress ?? string.Empty);
                EditorGUILayout.TextField("Compiled Blueprint Address", Package.CompiledBlueprintAddress ?? string.Empty);
                EditorGUILayout.TextField("Address", MetaAsset.EntityAddress ?? string.Empty);
                EditorGUILayout.TextField("AssetPath", MetaAsset.EntityAssetPath ?? string.Empty);
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("ExtraSfxAddress", Package.ExtraSfxAddress ?? string.Empty);
            SfxClip = (AudioClip)EditorGUILayout.ObjectField("Sfx Clip", SfxClip, typeof(AudioClip), false);
            if (SfxClip != null)
                Package.ExtraSfxAddress = EntityDataEditorUtility.EnsureAddressableAddressForAsset(SfxClip);

            Package.Version = EditorGUILayout.IntField("Version", Package.Version);
            Package.IsDirty = EditorGUILayout.Toggle("IsDirty", Package.IsDirty);
        }

        private void DrawBlueprintSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("EntityBlueprintRef", EditorStyles.boldLabel);
            Package.EntityBlueprintAddress = EditorGUILayout.TextField("Blueprint Address", Package.EntityBlueprintAddress ?? string.Empty);
            Package.CompiledBlueprintAddress = EditorGUILayout.TextField("Compiled Blueprint Address", Package.CompiledBlueprintAddress ?? string.Empty);

            bool sourceValid = EntityDataEditorUtility.IsBlueprintAddressValid(Package.EntityBlueprintAddress);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(sourceValid))
            {
                if (GUILayout.Button("选择实体行为蓝图"))
                {
                    SaveBeforeBlueprintAction("SelectBlueprint");
                    if (EntityDataEditorUtility.TryPickBlueprintAddress(out string address))
                    {
                        Package.EntityBlueprintAddress = address;
                        SaveNow("SelectBlueprintSaved", true);
                    }
                }

                if (GUILayout.Button("新建实体行为蓝图"))
                {
                    SaveBeforeBlueprintAction("CreateBlueprint");
                    Package.EntityBlueprintAddress = EntityDataEditorUtility.CreateAndOpenBlueprint(Package.EntityIdToken);
                    SaveNow("CreateBlueprintSaved", true);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("编辑实体蓝图"))
            {
                SaveBeforeBlueprintAction("EditBlueprint");
                bool ok = EntityDataEditorUtility.OpenBlueprintByAddress(Package.EntityBlueprintAddress);
                SaveNow("EditBlueprintSaved", true);
                if (!ok)
                    EditorUtility.DisplayDialog("提示", "当前蓝图 Address 无效或文件不存在。", "确定");
            }

            if (GUILayout.Button("编译蓝图"))
            {
                SaveBeforeBlueprintAction("CompileBlueprint");
                if (EntityDataEditorUtility.TryCompileBlueprintByToken(Package.EntityIdToken, out string compileDetail))
                {
                    LoadNow();
                    SaveNow("CompileBlueprintSaved", true);
                    EditorUtility.DisplayDialog("编译蓝图", compileDetail, "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("编译失败", compileDetail, "确定");
                }
            }

            if (GUILayout.Button("静态实例化"))
            {
                SaveBeforeBlueprintAction("StaticInstantiateBlueprint");
                if (EntityDataEditorUtility.TryInstantiateFromCompiledBlueprintByToken(Package.EntityIdToken, out string instantiateDetail))
                {
                    EditorUtility.DisplayDialog("静态实例化", instantiateDetail, "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("静态实例化失败", instantiateDetail, "确定");
                }
            }

            if (sourceValid)
            {
                EditorGUILayout.HelpBox("蓝图 Address 有效，已禁用【选择/新建】按钮，仅允许编辑。", MessageType.Info);
            }

            EditorGUILayout.HelpBox(EntityDataEditorUtility.GetBlueprintSummary(Package.EntityBlueprintAddress), MessageType.None);
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("保存"))
                SaveNow("ManualSave", true);

            if (GUILayout.Button("加载"))
                LoadNow();

            EditorGUILayout.EndHorizontal();
        }

        private void MaybeAutoSave()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastAutoSaveAt < AutoSaveIntervalSeconds)
                return;

            _lastAutoSaveAt = now;
            SaveNow("AutoSave", false);
        }

        private void SaveBeforeBlueprintAction(string reason)
        {
            SaveNow(reason, true);
            Debug.Log($"[EntityData] Pre-blueprint action saved: {reason}, token={Package.EntityIdToken}");
        }
    }

}
#endif


