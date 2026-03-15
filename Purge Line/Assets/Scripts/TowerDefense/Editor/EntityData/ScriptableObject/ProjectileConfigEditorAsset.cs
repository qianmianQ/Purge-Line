#if UNITY_EDITOR
using TowerDefense.Data.EntityData;

namespace TowerDefense.Editor.EntityData
{
    public sealed class ProjectileConfigEditorAsset : EntityConfigEditorAssetBase
    {
        public ProjectileConfigPackage CurrentConfig = new ProjectileConfigPackage();

        public override IEntityConfigPackage GetPackage() => CurrentConfig;

        public void LoadFrom(EntityType entityType, int localId, string entityIdToken, string address, string assetPath,
            ProjectileConfigPackage source)
        {
            ApplyCommonMeta(entityType, localId, entityIdToken, address, assetPath);
            CurrentConfig = source ?? new ProjectileConfigPackage();
            CurrentConfig.EntityIdToken = entityIdToken;
            CurrentConfig.Normalize();
        }
    }
}
#endif

