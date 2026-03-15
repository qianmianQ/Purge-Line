#if UNITY_EDITOR
using TowerDefense.Data.EntityData;
using UnityEngine;

namespace TowerDefense.Editor.EntityData
{
    public abstract class EntityConfigEditorAssetBase : ScriptableObject
    {
        public EntityType EntityType;
        public int LocalId;
        public string EntityIdToken;
        public string EntityAddress;
        public string EntityAssetPath;

        public abstract IEntityConfigPackage GetPackage();

        protected void ApplyCommonMeta(EntityType entityType, int localId, string entityIdToken, string address,
            string assetPath)
        {
            EntityType = entityType;
            LocalId = localId;
            EntityIdToken = entityIdToken;
            EntityAddress = address;
            EntityAssetPath = assetPath;
        }
    }

}
#endif

