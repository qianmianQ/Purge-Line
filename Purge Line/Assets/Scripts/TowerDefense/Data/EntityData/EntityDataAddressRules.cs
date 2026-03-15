using System.Text;

namespace TowerDefense.Data.EntityData
{
    public static class EntityDataAddressRules
    {
        public const string IndexAddress = "td/entity/index";

        public static string BuildEntityConfigAddress(EntityType entityType, string entityIdToken)
        {
            return $"td/entity/{ToSlug(entityType.ToString())}/{ToSlug(entityIdToken)}";
        }

        public static string BuildDefaultSpriteAddress(string assetName)
        {
            return $"td/ui/sprite/{ToSlug(assetName)}";
        }

        public static string ToSlug(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "unknown";

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = char.ToLowerInvariant(raw[i]);
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            string normalized = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
        }

        public static bool TryBuildToken(EntityType entityType, int localId, out string token)
        {
            if (localId <= 0 || entityType == EntityType.Max)
            {
                token = string.Empty;
                return false;
            }

            token = $"{entityType}_{localId}";
            return true;
        }
    }
}


