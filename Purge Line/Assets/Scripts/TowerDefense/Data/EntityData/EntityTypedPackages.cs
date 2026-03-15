using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class TurretConfigPackage : IEntityConfigPackage
    {
        private enum FieldOrder : ushort
        {
            // 基础标识
            EntityIdToken = 1,
            Base = 2,

            // UI数据
            Ui = 50,

            // 蓝图资源
            EntityBlueprintAddress = 100,
            CompiledBlueprintAddress = 110,
            ExtraSfxAddress = 150,

            // 版本控制
            Version = 200,
            IsDirty = 201,
            SchemaVersion = 202
        }

        public const int CurrentSchemaVersion = 3;

        [MemoryPackOrder((ushort)FieldOrder.EntityIdToken)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Base)] public TurretBaseData Base { get; set; } = new TurretBaseData();
        [MemoryPackOrder((ushort)FieldOrder.Ui)] public TurretUIData Ui { get; set; } = new TurretUIData();
        [MemoryPackOrder((ushort)FieldOrder.EntityBlueprintAddress)] public string EntityBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.CompiledBlueprintAddress)] public string CompiledBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ExtraSfxAddress)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Version)] public int Version { get; set; } = 1;
        [MemoryPackOrder((ushort)FieldOrder.IsDirty)] public bool IsDirty { get; set; }
        [MemoryPackOrder((ushort)FieldOrder.SchemaVersion)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.TURRET;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new TurretBaseData();
            Ui ??= new TurretUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintAddress ??= string.Empty;
            CompiledBlueprintAddress ??= string.Empty;
            ExtraSfxAddress ??= string.Empty;
            Base.Name ??= string.Empty;
            Base.Description ??= string.Empty;
            Ui.DisplayName ??= string.Empty;
            Ui.Description ??= string.Empty;
            Ui.IconAddress ??= string.Empty;
            Ui.PreviewAddress ??= string.Empty;
            Ui.ThemeColorHex ??= "#FFFFFFFF";
            if (Version <= 0) Version = 1;
            if (SchemaVersion <= 0) SchemaVersion = 1;
        }

        public static TurretConfigPackage BuildFallback(string token, string reason)
        {
            return new TurretConfigPackage
            {
                EntityIdToken = token,
                Base = new TurretBaseData { Name = token, Description = reason },
                Ui = new TurretUIData { DisplayName = token, Description = reason, ThemeColorHex = "#FF0000FF" },
                IsDirty = true
            };
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EnemyConfigPackage : IEntityConfigPackage
    {
        private enum FieldOrder : ushort
        {
            // 基础标识
            EntityIdToken = 1,
            Base = 2,

            // UI数据
            Ui = 50,

            // 蓝图资源
            EntityBlueprintAddress = 100,
            CompiledBlueprintAddress = 110,
            ExtraSfxAddress = 150,

            // 版本控制
            Version = 200,
            IsDirty = 201,
            SchemaVersion = 202
        }

        public const int CurrentSchemaVersion = 3;

        [MemoryPackOrder((ushort)FieldOrder.EntityIdToken)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Base)] public EnemyBaseData Base { get; set; } = new EnemyBaseData();
        [MemoryPackOrder((ushort)FieldOrder.Ui)] public EnemyUIData Ui { get; set; } = new EnemyUIData();
        [MemoryPackOrder((ushort)FieldOrder.EntityBlueprintAddress)] public string EntityBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.CompiledBlueprintAddress)] public string CompiledBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ExtraSfxAddress)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Version)] public int Version { get; set; } = 1;
        [MemoryPackOrder((ushort)FieldOrder.IsDirty)] public bool IsDirty { get; set; }
        [MemoryPackOrder((ushort)FieldOrder.SchemaVersion)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.ENEMY;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new EnemyBaseData();
            Ui ??= new EnemyUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintAddress ??= string.Empty;
            CompiledBlueprintAddress ??= string.Empty;
            ExtraSfxAddress ??= string.Empty;
            Base.Name ??= string.Empty;
            Base.Description ??= string.Empty;
            Ui.DisplayName ??= string.Empty;
            Ui.Description ??= string.Empty;
            Ui.IconAddress ??= string.Empty;
            Ui.PreviewAddress ??= string.Empty;
            Ui.ThemeColorHex ??= "#FFFFFFFF";
            if (Version <= 0) Version = 1;
            if (SchemaVersion <= 0) SchemaVersion = 1;
        }

        public static EnemyConfigPackage BuildFallback(string token, string reason)
        {
            return new EnemyConfigPackage
            {
                EntityIdToken = token,
                Base = new EnemyBaseData { Name = token, Description = reason },
                Ui = new EnemyUIData { DisplayName = token, Description = reason, ThemeColorHex = "#FF0000FF" },
                IsDirty = true
            };
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ProjectileConfigPackage : IEntityConfigPackage
    {
        private enum FieldOrder : ushort
        {
            // 基础标识
            EntityIdToken = 1,
            Base = 2,

            // UI数据
            Ui = 50,

            // 蓝图资源
            EntityBlueprintAddress = 100,
            CompiledBlueprintAddress = 110,
            ExtraSfxAddress = 150,

            // 版本控制
            Version = 200,
            IsDirty = 201,
            SchemaVersion = 202
        }

        public const int CurrentSchemaVersion = 3;

        [MemoryPackOrder((ushort)FieldOrder.EntityIdToken)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Base)] public ProjectileBaseData Base { get; set; } = new ProjectileBaseData();
        [MemoryPackOrder((ushort)FieldOrder.Ui)] public ProjectileUIData Ui { get; set; } = new ProjectileUIData();
        [MemoryPackOrder((ushort)FieldOrder.EntityBlueprintAddress)] public string EntityBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.CompiledBlueprintAddress)] public string CompiledBlueprintAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ExtraSfxAddress)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Version)] public int Version { get; set; } = 1;
        [MemoryPackOrder((ushort)FieldOrder.IsDirty)] public bool IsDirty { get; set; }
        [MemoryPackOrder((ushort)FieldOrder.SchemaVersion)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.PROJECTILE;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new ProjectileBaseData();
            Ui ??= new ProjectileUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintAddress ??= string.Empty;
            CompiledBlueprintAddress ??= string.Empty;
            ExtraSfxAddress ??= string.Empty;
            Base.Name ??= string.Empty;
            Base.Description ??= string.Empty;
            Ui.DisplayName ??= string.Empty;
            Ui.Description ??= string.Empty;
            Ui.IconAddress ??= string.Empty;
            Ui.PreviewAddress ??= string.Empty;
            Ui.ThemeColorHex ??= "#FFFFFFFF";
            if (Version <= 0) Version = 1;
            if (SchemaVersion <= 0) SchemaVersion = 1;
        }

        public static ProjectileConfigPackage BuildFallback(string token, string reason)
        {
            return new ProjectileConfigPackage
            {
                EntityIdToken = token,
                Base = new ProjectileBaseData { Name = token, Description = reason },
                Ui = new ProjectileUIData { DisplayName = token, Description = reason, ThemeColorHex = "#FF0000FF" },
                IsDirty = true
            };
        }
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class TurretBaseData
    {
        private enum FieldOrder : ushort
        {
            Name = 1,
            Description = 2,
            Cost = 3,
            MaxHp = 4,
            AttackRange = 5,
            AttackInterval = 6
        }

        [MemoryPackOrder((ushort)FieldOrder.Name)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Cost)] public int Cost { get; set; }
        [MemoryPackOrder((ushort)FieldOrder.MaxHp)] public float MaxHp { get; set; } = 1f;
        [MemoryPackOrder((ushort)FieldOrder.AttackRange)] public float AttackRange { get; set; } = 3f;
        [MemoryPackOrder((ushort)FieldOrder.AttackInterval)] public float AttackInterval { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EnemyBaseData
    {
        private enum FieldOrder : ushort
        {
            Name = 1,
            Description = 2,
            Reward = 3,
            MaxHp = 4,
            MoveSpeed = 5
        }

        [MemoryPackOrder((ushort)FieldOrder.Name)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Reward)] public int Reward { get; set; }
        [MemoryPackOrder((ushort)FieldOrder.MaxHp)] public float MaxHp { get; set; } = 1f;
        [MemoryPackOrder((ushort)FieldOrder.MoveSpeed)] public float MoveSpeed { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ProjectileBaseData
    {
        private enum FieldOrder : ushort
        {
            Name = 1,
            Description = 2,
            Speed = 3,
            LifeTime = 4,
            Damage = 5
        }

        [MemoryPackOrder((ushort)FieldOrder.Name)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Speed)] public float Speed { get; set; } = 5f;
        [MemoryPackOrder((ushort)FieldOrder.LifeTime)] public float LifeTime { get; set; } = 2f;
        [MemoryPackOrder((ushort)FieldOrder.Damage)] public float Damage { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class TurretUIData
    {
        private enum FieldOrder : ushort
        {
            DisplayName = 50,
            Description = 51,
            IconAddress = 52,
            PreviewAddress = 53,
            ThemeColorHex = 54
        }

        [MemoryPackOrder((ushort)FieldOrder.DisplayName)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.IconAddress)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.PreviewAddress)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ThemeColorHex)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EnemyUIData
    {
        private enum FieldOrder : ushort
        {
            DisplayName = 50,
            Description = 51,
            IconAddress = 52,
            PreviewAddress = 53,
            ThemeColorHex = 54
        }

        [MemoryPackOrder((ushort)FieldOrder.DisplayName)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.IconAddress)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.PreviewAddress)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ThemeColorHex)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ProjectileUIData
    {
        private enum FieldOrder : ushort
        {
            DisplayName = 50,
            Description = 51,
            IconAddress = 52,
            PreviewAddress = 53,
            ThemeColorHex = 54
        }

        [MemoryPackOrder((ushort)FieldOrder.DisplayName)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.Description)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.IconAddress)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.PreviewAddress)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder((ushort)FieldOrder.ThemeColorHex)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }
}
