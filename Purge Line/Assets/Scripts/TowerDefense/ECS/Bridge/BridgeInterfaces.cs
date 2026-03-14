using Cysharp.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TowerDefense.Bridge
{
    public interface IGridBridgeSystem
    {
        string CurrentLevelId { get; }
        bool IsMapLoaded { get; }
        bool LoadLevel(string levelId);
        bool CanPlaceAt(int2 gridCoord);
        bool PlaceTower(int2 gridCoord, Entity towerEntity);
        int2 WorldToGrid(float2 worldPos);
        float2 GridToWorld(int2 gridCoord);
    }

    public interface ICombatBridgeSystem
    {
        bool IsCombatReady { get; }
        Entity CreateTower(int2 gridCoord);
    }
}

namespace TowerDefense.ECS.Bridge
{
    public interface IEcsVisualBridgeSystem
    {
        void InitEntitiesPools(string[] addresses);
        GameObject GetGameObjectInPoolSync(string address);
        UniTask<GameObject> GetGameObjectInPoolASync(string address);
        void ReturnGameObjectInPool(GameObject obj, string address = null);
    }
}
