#if UNITY_EDITOR
using TowerDefense.Data.EntityData;

namespace TowerDefense.Editor.EntityData
{
    public sealed class TurretConfigEditorAsset : EntityConfigEditorAssetBase
    {
        public TurretConfigPackage CurrentConfig = new TurretConfigPackage();

        public override IEntityConfigPackage GetPackage() => CurrentConfig;

        public void LoadFrom(EntityType entityType, int localId, string entityIdToken, string address, string assetPath,
            TurretConfigPackage source)
        {
            ApplyCommonMeta(entityType, localId, entityIdToken, address, assetPath);
            CurrentConfig = source ?? new TurretConfigPackage();
            CurrentConfig.EntityIdToken = entityIdToken;
            CurrentConfig.Normalize();
        }
    }
}
#endif

