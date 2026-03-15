using MemoryPack;

namespace TowerDefense.Data.EntityData
{
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class TurretConfigPackage : IEntityConfigPackage
    {
        public const int CurrentSchemaVersion = 2;

        [MemoryPackOrder(1)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public TurretBaseData Base { get; set; } = new TurretBaseData();
        [MemoryPackOrder(50)] public TurretUIData Ui { get; set; } = new TurretUIData();
        [MemoryPackOrder(100)] public string EntityBlueprintGuid { get; set; } = string.Empty;
        [MemoryPackOrder(150)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder(200)] public int Version { get; set; } = 1;
        [MemoryPackOrder(201)] public bool IsDirty { get; set; }
        [MemoryPackOrder(202)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.TURRET;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new TurretBaseData();
            Ui ??= new TurretUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintGuid ??= string.Empty;
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
        public const int CurrentSchemaVersion = 2;

        [MemoryPackOrder(1)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public EnemyBaseData Base { get; set; } = new EnemyBaseData();
        [MemoryPackOrder(50)] public EnemyUIData Ui { get; set; } = new EnemyUIData();
        [MemoryPackOrder(100)] public string EntityBlueprintGuid { get; set; } = string.Empty;
        [MemoryPackOrder(150)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder(200)] public int Version { get; set; } = 1;
        [MemoryPackOrder(201)] public bool IsDirty { get; set; }
        [MemoryPackOrder(202)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.ENEMY;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new EnemyBaseData();
            Ui ??= new EnemyUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintGuid ??= string.Empty;
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
        public const int CurrentSchemaVersion = 2;

        [MemoryPackOrder(1)] public string EntityIdToken { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public ProjectileBaseData Base { get; set; } = new ProjectileBaseData();
        [MemoryPackOrder(50)] public ProjectileUIData Ui { get; set; } = new ProjectileUIData();
        [MemoryPackOrder(100)] public string EntityBlueprintGuid { get; set; } = string.Empty;
        [MemoryPackOrder(150)] public string ExtraSfxAddress { get; set; } = string.Empty;
        [MemoryPackOrder(200)] public int Version { get; set; } = 1;
        [MemoryPackOrder(201)] public bool IsDirty { get; set; }
        [MemoryPackOrder(202)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [MemoryPackIgnore] public EntityType EntityType => EntityType.PROJECTILE;
        [MemoryPackIgnore] public string DisplayNameForLog => Ui?.DisplayName ?? Base?.Name ?? EntityIdToken;

        public void Normalize()
        {
            Base ??= new ProjectileBaseData();
            Ui ??= new ProjectileUIData();
            EntityIdToken ??= string.Empty;
            EntityBlueprintGuid ??= string.Empty;
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
        [MemoryPackOrder(1)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(3)] public int Cost { get; set; }
        [MemoryPackOrder(4)] public float MaxHp { get; set; } = 1f;
        [MemoryPackOrder(5)] public float AttackRange { get; set; } = 3f;
        [MemoryPackOrder(6)] public float AttackInterval { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EnemyBaseData
    {
        [MemoryPackOrder(1)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(3)] public int Reward { get; set; }
        [MemoryPackOrder(4)] public float MaxHp { get; set; } = 1f;
        [MemoryPackOrder(5)] public float MoveSpeed { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ProjectileBaseData
    {
        [MemoryPackOrder(1)] public string Name { get; set; } = string.Empty;
        [MemoryPackOrder(2)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(3)] public float Speed { get; set; } = 5f;
        [MemoryPackOrder(4)] public float LifeTime { get; set; } = 2f;
        [MemoryPackOrder(5)] public float Damage { get; set; } = 1f;
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class TurretUIData
    {
        [MemoryPackOrder(50)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder(51)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(52)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder(53)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder(54)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class EnemyUIData
    {
        [MemoryPackOrder(50)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder(51)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(52)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder(53)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder(54)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }

    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class ProjectileUIData
    {
        [MemoryPackOrder(50)] public string DisplayName { get; set; } = string.Empty;
        [MemoryPackOrder(51)] public string Description { get; set; } = string.Empty;
        [MemoryPackOrder(52)] public string IconAddress { get; set; } = string.Empty;
        [MemoryPackOrder(53)] public string PreviewAddress { get; set; } = string.Empty;
        [MemoryPackOrder(54)] public string ThemeColorHex { get; set; } = "#FFFFFFFF";
    }
}

