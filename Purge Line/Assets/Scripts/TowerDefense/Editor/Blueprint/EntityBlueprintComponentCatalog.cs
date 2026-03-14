#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;

namespace TowerDefense.Editor.Blueprint
{
    internal sealed class EntityBlueprintComponentCatalog
    {
        private const string FavoritesKey = "TowerDefense.EntityBlueprint.Favorites";

        private readonly List<ComponentTypeInfo> _allComponents = new List<ComponentTypeInfo>();
        private readonly HashSet<string> _favorites = new HashSet<string>();

        public IReadOnlyList<ComponentTypeInfo> AllComponents => _allComponents;

        public EntityBlueprintComponentCatalog()
        {
            Rebuild();
        }

        public void InitializeFromEditorPrefs()
        {
            LoadFavorites();
            Rebuild();
        }

        public void Rebuild()
        {
            _allComponents.Clear();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null || !type.IsValueType || type.IsEnum || type.IsPrimitive)
                        continue;
                    if (type.IsGenericType || type.IsAbstract)
                        continue;
                    if (!typeof(IComponentData).IsAssignableFrom(type))
                        continue;

                    string aqn = type.AssemblyQualifiedName;
                    if (string.IsNullOrEmpty(aqn))
                        continue;

                    _allComponents.Add(new ComponentTypeInfo(type, ResolveCategory(type), _favorites.Contains(aqn)));
                }
            }

            _allComponents.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<ComponentTypeInfo> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return _allComponents;

            string normalized = keyword.Trim();
            return _allComponents
                .Where(x => x.DisplayName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        public IReadOnlyList<string> Categories()
        {
            return _allComponents.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        }

        public IReadOnlyList<ComponentTypeInfo> GetByCategory(string category)
        {
            return _allComponents
                .Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public IReadOnlyList<ComponentTypeInfo> Favorites()
        {
            return _allComponents.Where(x => x.IsFavorite).OrderBy(x => x.DisplayName).ToList();
        }

        public void ToggleFavorite(ComponentTypeInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.AssemblyQualifiedName))
                return;

            if (!_favorites.Add(info.AssemblyQualifiedName))
                _favorites.Remove(info.AssemblyQualifiedName);

            PersistFavorites();
            Rebuild();
        }

        private static string ResolveCategory(Type type)
        {
            string fullName = type.FullName ?? type.Name;
            if (fullName.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Attributes/Health";
            if (fullName.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullName.IndexOf("Position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullName.IndexOf("Velocity", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Movement";
            if (fullName.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullName.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullName.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Combat/Attack";
            if (fullName.IndexOf("Tag", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Tags";

            string ns = type.Namespace ?? "General";
            int lastDot = ns.LastIndexOf('.');
            return lastDot > 0 ? ns.Substring(lastDot + 1) : ns;
        }

        private void LoadFavorites()
        {
            _favorites.Clear();
            string raw = UnityEditor.EditorPrefs.GetString(FavoritesKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i];
                if (!string.IsNullOrEmpty(item))
                    _favorites.Add(item);
            }
        }

        private void PersistFavorites()
        {
            UnityEditor.EditorPrefs.SetString(FavoritesKey, string.Join(";", _favorites));
        }
    }

    internal sealed class ComponentTypeInfo
    {
        public Type Type { get; }
        public string DisplayName { get; }
        public string FullName { get; }
        public string AssemblyQualifiedName { get; }
        public string Category { get; }
        public bool IsFavorite { get; }

        public ComponentTypeInfo(Type type, string category, bool isFavorite)
        {
            Type = type;
            DisplayName = type.Name;
            FullName = type.FullName ?? type.Name;
            AssemblyQualifiedName = type.AssemblyQualifiedName;
            Category = string.IsNullOrEmpty(category) ? "General" : category;
            IsFavorite = isFavorite;
        }
    }
}
#endif

