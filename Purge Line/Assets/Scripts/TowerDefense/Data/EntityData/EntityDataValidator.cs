using System;
using System.Collections.Generic;
using System.Text;

namespace TowerDefense.Data.EntityData
{
    /// <summary>
    /// 实体数据配置验证器
    /// 用于验证配置数据的完整性和有效性
    /// </summary>
    public static class EntityDataValidator
    {
        /// <summary>
        /// 验证结果
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public void AddError(string message) => Errors.Add(message);
            public void AddWarning(string message) => Warnings.Add(message);

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"验证结果: {(IsValid ? "通过" : "失败")}");
                sb.AppendLine($"错误数: {Errors.Count}, 警告数: {Warnings.Count}");

                if (Errors.Count > 0)
                {
                    sb.AppendLine("\n错误:");
                    foreach (var error in Errors)
                        sb.AppendLine($"  - {error}");
                }

                if (Warnings.Count > 0)
                {
                    sb.AppendLine("\n警告:");
                    foreach (var warning in Warnings)
                        sb.AppendLine($"  - {warning}");
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 验证单个配置包
        /// </summary>
        public static ValidationResult Validate(IEntityConfigPackage package)
        {
            var result = new ValidationResult();

            if (package == null)
            {
                result.AddError("配置包为 null");
                return result;
            }

            // 验证必填字段
            if (string.IsNullOrWhiteSpace(package.EntityIdToken))
                result.AddError("EntityIdToken 不能为空");

            // 验证版本号
            if (package.Version < 0)
                result.AddError($"Version 不能为负数: {package.Version}");

            // 验证 Schema 版本
            if (package.SchemaVersion < 0)
                result.AddError($"SchemaVersion 不能为负数: {package.SchemaVersion}");

            // 类型特定验证
            ValidateTypeSpecific(package, result);

            return result;
        }

        /// <summary>
        /// 批量验证多个配置包
        /// </summary>
        public static ValidationResult ValidateBatch(IEnumerable<IEntityConfigPackage> packages)
        {
            var result = new ValidationResult();
            int index = 0;

            foreach (var package in packages)
            {
                var packageResult = Validate(package);
                if (!packageResult.IsValid)
                {
                    foreach (var error in packageResult.Errors)
                        result.AddError($"[{index}] {error}");
                }
                foreach (var warning in packageResult.Warnings)
                    result.AddWarning($"[{index}] {warning}");

                index++;
            }

            return result;
        }

        /// <summary>
        /// 验证炮塔配置特定规则
        /// </summary>
        private static void ValidateTurretConfig(TurretConfigPackage turret, ValidationResult result)
        {
            if (turret.Base == null)
            {
                result.AddError("TurretBaseData 为 null");
                return;
            }

            // 成本验证
            if (turret.Base.Cost < 0)
                result.AddError($"Cost 不能为负数: {turret.Base.Cost}");

            // 生命值验证
            if (turret.Base.MaxHp <= 0)
                result.AddError($"MaxHp 必须大于 0: {turret.Base.MaxHp}");

            // 攻击范围验证
            if (turret.Base.AttackRange <= 0)
                result.AddError($"AttackRange 必须大于 0: {turret.Base.AttackRange}");

            // 攻击间隔验证
            if (turret.Base.AttackInterval <= 0)
                result.AddError($"AttackInterval 必须大于 0: {turret.Base.AttackInterval}");

            // 警告：数值过大
            if (turret.Base.Cost > 10000)
                result.AddWarning($"Cost 数值过大: {turret.Base.Cost}");
            if (turret.Base.MaxHp > 100000)
                result.AddWarning($"MaxHp 数值过大: {turret.Base.MaxHp}");
        }

        /// <summary>
        /// 验证敌人配置特定规则
        /// </summary>
        private static void ValidateEnemyConfig(EnemyConfigPackage enemy, ValidationResult result)
        {
            if (enemy.Base == null)
            {
                result.AddError("EnemyBaseData 为 null");
                return;
            }

            // 奖励验证
            if (enemy.Base.Reward < 0)
                result.AddError($"Reward 不能为负数: {enemy.Base.Reward}");

            // 生命值验证
            if (enemy.Base.MaxHp <= 0)
                result.AddError($"MaxHp 必须大于 0: {enemy.Base.MaxHp}");

            // 移动速度验证
            if (enemy.Base.MoveSpeed <= 0)
                result.AddError($"MoveSpeed 必须大于 0: {enemy.Base.MoveSpeed}");

            // 警告：数值过大
            if (enemy.Base.Reward > 10000)
                result.AddWarning($"Reward 数值过大: {enemy.Base.Reward}");
            if (enemy.Base.MaxHp > 1000000)
                result.AddWarning($"MaxHp 数值过大（可能是Boss？）: {enemy.Base.MaxHp}");
        }

        /// <summary>
        /// 验证投射物配置特定规则
        /// </summary>
        private static void ValidateProjectileConfig(ProjectileConfigPackage projectile, ValidationResult result)
        {
            if (projectile.Base == null)
            {
                result.AddError("ProjectileBaseData 为 null");
                return;
            }

            // 速度验证
            if (projectile.Base.Speed <= 0)
                result.AddError($"Speed 必须大于 0: {projectile.Base.Speed}");

            // 生命周期验证
            if (projectile.Base.LifeTime <= 0)
                result.AddError($"LifeTime 必须大于 0: {projectile.Base.LifeTime}");

            // 伤害验证（可以为0，但警告）
            if (projectile.Base.Damage < 0)
                result.AddError($"Damage 不能为负数: {projectile.Base.Damage}");
            else if (projectile.Base.Damage == 0)
                result.AddWarning("Damage 为 0（这是预期的吗？）");

            // 警告：数值过大
            if (projectile.Base.Speed > 100)
                result.AddWarning($"Speed 数值过大: {projectile.Base.Speed}");
        }

        /// <summary>
        /// 根据类型执行特定的验证
        /// </summary>
        private static void ValidateTypeSpecific(IEntityConfigPackage package, ValidationResult result)
        {
            switch (package.EntityType)
            {
                case EntityType.TURRET:
                    if (package is TurretConfigPackage turret)
                        ValidateTurretConfig(turret, result);
                    break;

                case EntityType.ENEMY:
                    if (package is EnemyConfigPackage enemy)
                        ValidateEnemyConfig(enemy, result);
                    break;

                case EntityType.PROJECTILE:
                    if (package is ProjectileConfigPackage projectile)
                        ValidateProjectileConfig(projectile, result);
                    break;

                default:
                    result.AddWarning($"未知的实体类型: {package.EntityType}");
                    break;
            }

            // UI数据通用验证
            ValidateUIData(package, result);
        }

        /// <summary>
        /// 验证UI数据
        /// </summary>
        private static void ValidateUIData(IEntityConfigPackage package, ValidationResult result)
        {
            // 获取UI数据（通过反射，因为不同类型有不同的UI数据类型）
            object uiData = null;

            switch (package)
            {
                case TurretConfigPackage turret:
                    uiData = turret.Ui;
                    break;
                case EnemyConfigPackage enemy:
                    uiData = enemy.Ui;
                    break;
                case ProjectileConfigPackage projectile:
                    uiData = projectile.Ui;
                    break;
            }

            if (uiData == null)
            {
                result.AddWarning("UI 数据为 null");
                return;
            }

            // 检查主题颜色格式（如果存在）
            var themeColorProperty = uiData.GetType().GetProperty("ThemeColorHex");
            if (themeColorProperty != null)
            {
                var colorHex = themeColorProperty.GetValue(uiData) as string;
                if (!string.IsNullOrEmpty(colorHex) && !IsValidColorHex(colorHex))
                {
                    result.AddWarning($"ThemeColorHex 格式可能不正确: {colorHex} (应为 #RRGGBB 或 #RRGGBBAA)");
                }
            }
        }

        /// <summary>
        /// 验证颜色十六进制格式
        /// </summary>
        private static bool IsValidColorHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#"))
                return false;

            var digits = hex.Substring(1);
            return digits.Length == 6 || digits.Length == 8; // RGB or RGBA
        }
    }
}
