// GamePlayEvents.cs — Gameplay 域有参事件数据结构定义区
// 在此定义 Gameplay 域使用的 struct 事件，例如：
//
// public readonly struct TowerPlacedEvent
// {
//     public readonly int GridX;
//     public readonly int GridY;
//     public TowerPlacedEvent(int x, int y) { GridX = x; GridY = y; }
// }

using Unity.Entities;
using Unity.Mathematics;

namespace PurgeLine.Events
{
    // ── 事件定义 ──────────────────────────────────────────────

    /// <summary>地图加载完成事件</summary>
    public struct GridMapLoadedEvent
    {
        public string LevelId;
        public int Width;
        public int Height;
        public float CellSize;
    }
    
    /// <summary>格子状态变更事件</summary>
    public struct GridCellChangedEvent
    {
        public int2 GridCoord;
        public CellChangeType ChangeType;
        public Entity Entity;
    }
    
    /// <summary>流场烘焙完成事件</summary>
    public struct FlowFieldBakedEvent
    {
        public int Width;
        public int Height;
        public int GoalCount;
    }
    
    /// <summary>格子变更类型</summary>
    public enum CellChangeType
    {
        TowerPlaced,
        TowerRemoved,
        TypeChanged
    }
}
