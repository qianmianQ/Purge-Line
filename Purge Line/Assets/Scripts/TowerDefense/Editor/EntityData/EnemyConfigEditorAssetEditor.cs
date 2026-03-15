#if UNITY_EDITOR
using TowerDefense.Data.EntityData;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    [CustomEditor(typeof(EnemyConfigEditorAsset))]
    public sealed class EnemyConfigEditorAssetEditor : EntityConfigEditorInspectorBase
    {
        private EnemyConfigEditorAsset TypedTarget => (EnemyConfigEditorAsset)target;
        protected override EntityConfigEditorAssetBase MetaAsset => TypedTarget;
        protected override IEntityConfigPackage Package => TypedTarget.CurrentConfig ??= new EnemyConfigPackage();

        protected override void DrawEditorHeader()
        {
            EditorGUILayout.LabelField("Enemy Config Editor", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("EntityType", TypedTarget.EntityType);
                EditorGUILayout.IntField("LocalId", TypedTarget.LocalId);
            }
        }

        protected override void DrawTypeSpecificFields()
        {
            var package = TypedTarget.CurrentConfig ??= new EnemyConfigPackage();
            package.Base ??= new EnemyBaseData();
            package.Ui ??= new EnemyUIData();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Enemy BaseData", EditorStyles.boldLabel);
            package.Base.Name = EditorGUILayout.TextField("Name", package.Base.Name ?? string.Empty);
            package.Base.Description = EditorGUILayout.TextField("Description", package.Base.Description ?? string.Empty);
            package.Base.Reward = EditorGUILayout.IntField("Reward", package.Base.Reward);
            package.Base.MaxHp = EditorGUILayout.FloatField("MaxHp", package.Base.MaxHp);
            package.Base.MoveSpeed = EditorGUILayout.FloatField("MoveSpeed", package.Base.MoveSpeed);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Enemy UIData", EditorStyles.boldLabel);
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

