#if UNITY_EDITOR
using System;
using System.Reflection;
using PurgeLine.Resource.Internal;
using UnityEngine;

namespace PurgeLine.Resource.Editor
{
    internal static class ResourceManagerResolver
    {
        public static ResourceManager TryGet()
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            try
            {
                var scopeType = FindType("GameLifetimeScope");
                if (scopeType == null)
                {
                    return null;
                }

                var instanceProp = scopeType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var scopeInstance = instanceProp?.GetValue(null);
                if (scopeInstance == null)
                {
                    return null;
                }

                var containerProp = scopeType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
                var container = containerProp?.GetValue(scopeInstance);
                if (container == null)
                {
                    return null;
                }

                var resolve = container.GetType().GetMethod("Resolve", new[] { typeof(Type), typeof(object) });
                if (resolve == null)
                {
                    return null;
                }

                var resolved = resolve.Invoke(container, new object[] { typeof(ResourceManager), null });
                return resolved as ResourceManager;
            }
            catch
            {
                return null;
            }
        }

        private static Type FindType(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(typeName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
#endif

