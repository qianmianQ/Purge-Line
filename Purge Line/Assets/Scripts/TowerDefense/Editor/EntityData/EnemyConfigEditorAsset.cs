#if UNITY_EDITOR
using TowerDefense.Data.EntityData;

namespace TowerDefense.Editor.EntityData
{
    public sealed class EnemyConfigEditorAsset : EntityConfigEditorAssetBase
    {
        public EnemyConfigPackage CurrentConfig = new EnemyConfigPackage();

        public override IEntityConfigPackage GetPackage() => CurrentConfig;

        public void LoadFrom(EntityType entityType, int localId, string entityIdToken, string address, string assetPath,
            EnemyConfigPackage source)
        {
            ApplyCommonMeta(entityType, localId, entityIdToken, address, assetPath);
            CurrentConfig = source ?? new EnemyConfigPackage();
            CurrentConfig.EntityIdToken = entityIdToken;
            CurrentConfig.Normalize();
        }
    }
}
#endif

