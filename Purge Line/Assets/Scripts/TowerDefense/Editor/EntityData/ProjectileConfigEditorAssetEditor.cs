#if UNITY_EDITOR
using TowerDefense.Data.EntityData;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    [CustomEditor(typeof(ProjectileConfigEditorAsset))]
    public sealed class ProjectileConfigEditorAssetEditor : EntityConfigEditorInspectorBase
    {
        private ProjectileConfigEditorAsset TypedTarget => (ProjectileConfigEditorAsset)target;
        protected override EntityConfigEditorAssetBase MetaAsset => TypedTarget;
        protected override IEntityConfigPackage Package => TypedTarget.CurrentConfig ??= new ProjectileConfigPackage();

        protected override void DrawEditorHeader()
        {
            EditorGUILayout.LabelField("Projectile Config Editor", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("EntityType", TypedTarget.EntityType);
                EditorGUILayout.IntField("LocalId", TypedTarget.LocalId);
            }
        }

        protected override void DrawTypeSpecificFields()
        {
            var package = TypedTarget.CurrentConfig ??= new ProjectileConfigPackage();
            package.Base ??= new ProjectileBaseData();
            package.Ui ??= new ProjectileUIData();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Projectile BaseData", EditorStyles.boldLabel);
            package.Base.Name = EditorGUILayout.TextField("Name", package.Base.Name ?? string.Empty);
            package.Base.Description = EditorGUILayout.TextField("Description", package.Base.Description ?? string.Empty);
            package.Base.Speed = EditorGUILayout.FloatField("Speed", package.Base.Speed);
            package.Base.LifeTime = EditorGUILayout.FloatField("LifeTime", package.Base.LifeTime);
            package.Base.Damage = EditorGUILayout.FloatField("Damage", package.Base.Damage);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Projectile UIData", EditorStyles.boldLabel);
            DrawSharedUiFields(package.Ui.DisplayName, package.Ui.Description, package.Ui.IconAddress, package.Ui.PreviewAddress,
                out var d, out var desc, out var icon, out var prev);
            package.Ui.DisplayName = d;
            package.Ui.Description = desc;
            package.Ui.IconAddress = icon;
            package.Ui.PreviewAddress = prev;
            package.Ui.ThemeColorHex = EditorGUILayout.TextField("ThemeColorHex", package.Ui.ThemeColorHex ?? "#FFFFFFFF");
        }

        protected override void SaveNow(string reason, bool verboseLog)
        {
            EntityDataEditorUtility.SaveTypedEditor(EntityDataEditorUtility.GetOrCreateRegistry(), TypedTarget);
            if (verboseLog)
                Debug.Log($"[EntityData] Saved({reason}): {TypedTarget.CurrentConfig.EntityIdToken}");
        }

        protected override void LoadNow()
        {
            var registry = EntityDataEditorUtility.GetOrCreateRegistry();
            var record = registry.MutableRecords.Find(x => x.EntityIdToken == TypedTarget.CurrentConfig.EntityIdToken);
            if (record != null)
                EntityDataEditorUtility.LoadIntoTypedEditor(record, TypedTarget);
        }
    }
}
#endif

