#if UNITY_INCLUDE_TESTS
using MemoryPack;
using UnityEngine;

namespace TowerDefense.Data
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class LevelConfigLegacyV1
    {
        private enum FieldOrder : ushort
        {
            LevelId = 0,
            Version = 1,
            Width = 50,
            Height = 51,
            CellSize = 52,
            OriginX = 53,
            OriginY = 54,
            Cells = 55,
            SpawnPoints = 100,
            GoalPoints = 101,
            DisplayName = 150,
            Description = 151
        }

        [MemoryPackOrder((ushort)FieldOrder.LevelId)]
        public string LevelId { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Version)]
        public int Version { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Width)]
        public int Width { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Height)]
        public int Height { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.CellSize)]
        public float CellSize { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.OriginX)]
        public float OriginX { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.OriginY)]
        public float OriginY { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Cells)]
        public byte[] Cells { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.SpawnPoints)]
        public Vector2[] SpawnPoints { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.GoalPoints)]
        public Vector2[] GoalPoints { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.DisplayName)]
        public string DisplayName { get; set; }

        [MemoryPackOrder((ushort)FieldOrder.Description)]
        public string Description { get; set; }
    }
}
#endif
