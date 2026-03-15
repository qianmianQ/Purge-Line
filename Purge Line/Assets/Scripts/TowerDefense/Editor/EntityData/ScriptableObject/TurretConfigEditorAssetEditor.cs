#if UNITY_EDITOR
using TowerDefense.Data.EntityData;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    [CustomEditor(typeof(TurretConfigEditorAsset))]
    public sealed class TurretConfigEditorAssetEditor : EntityConfigEditorInspectorBase
    {
        private TurretConfigEditorAsset TypedTarget => (TurretConfigEditorAsset)target;
        protected override EntityConfigEditorAssetBase MetaAsset => TypedTarget;
        protected override IEntityConfigPackage Package => TypedTarget.CurrentConfig ??= new TurretConfigPackage();

        protected override void DrawEditorHeader()
        {
            EditorGUILayout.LabelField("Turret Config Editor", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("EntityType", TypedTarget.EntityType);
                EditorGUILayout.IntField("LocalId", TypedTarget.LocalId);
            }
        }

        protected override void DrawTypeSpecificFields()
        {
            var package = TypedTarget.CurrentConfig ??= new TurretConfigPackage();
            package.Base ??= new TurretBaseData();
            package.Ui ??= new TurretUIData();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Turret BaseData", EditorStyles.boldLabel);
            package.Base.Name = EditorGUILayout.TextField("Name", package.Base.Name ?? string.Empty);
            package.Base.Description = EditorGUILayout.TextField("Description", package.Base.Description ?? string.Empty);
            package.Base.Cost = EditorGUILayout.IntField("Cost", package.Base.Cost);
            package.Base.MaxHp = EditorGUILayout.FloatField("MaxHp", package.Base.MaxHp);
            package.Base.AttackRange = EditorGUILayout.FloatField("AttackRange", package.Base.AttackRange);
            package.Base.AttackInterval = EditorGUILayout.FloatField("AttackInterval", package.Base.AttackInterval);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Turret UIData", EditorStyles.boldLabel);
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

