namespace TowerDefense.Data
{
    /// <summary>
    /// 战斗配置 — 静态数据类
    ///
    /// 集中管理所有战斗相关的默认参数。
    /// 后续可替换为 ScriptableObject 或 MemoryPack 序列化数据。
    /// </summary>
    public static class CombatConfig
    {
        // ── 炮塔默认参数 ─────────────────────────────────────

        /// <summary>默认攻击范围（世界单位）</summary>
        public const float DefaultTowerRange = 3.5f;

        /// <summary>默认攻击间隔（秒）</summary>
        public const float DefaultTowerInterval = 1.0f;

        /// <summary>默认伤害值</summary>
        public const int DefaultTowerDamage = 10;

        /// <summary>默认子弹速度（世界单位/秒）</summary>
        public const float DefaultBulletSpeed = 12.0f;

        /// <summary>默认炮塔建造费用</summary>
        public const int DefaultTowerCost = 50;

        // ── 敌人默认参数 ─────────────────────────────────────

        /// <summary>默认敌人 HP</summary>
        public const int DefaultEnemyHP = 100;

        /// <summary>默认敌人移动速度</summary>
        public const float DefaultEnemySpeed = 2.0f;

        /// <summary>默认敌人击杀奖励</summary>
        public const int DefaultEnemyReward = 10;

        // ── 子弹参数 ─────────────────────────────────────────

        /// <summary>子弹命中检测半径</summary>
        public const float BulletHitRadius = 0.25f;

        // ── 敌人生成参数 ─────────────────────────────────────

        /// <summary>敌人生成间隔（秒）</summary>
        public const float EnemySpawnInterval = 0.5f;

        /// <summary>单次生成数量</summary>
        public const int EnemySpawnBatchSize = 1;

        /// <summary>最大生成数量（0=无限，用于测试）</summary>
        public const int EnemyMaxSpawnCount = 200;

        // ── 炮塔升级参数（预留） ─────────────────────────────

        /// <summary>升级攻击范围增幅</summary>
        public const float UpgradeRangeIncrease = 0.5f;

        /// <summary>升级攻击间隔减少比例</summary>
        public const float UpgradeIntervalDecrease = 0.1f;

        /// <summary>升级伤害增幅</summary>
        public const int UpgradeDamageIncrease = 5;
    }
}

