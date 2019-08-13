using System;
using System.Collections.Concurrent;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures
{
    internal class RootComponentTypeCache
    {
        private readonly ConcurrentDictionary<string, Type> _identifierToType = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<Type, string> _typeToIdentifier = new ConcurrentDictionary<Type, string>();

        public string RegisterRootComponent(Type type)
        {
            // 'N' in ToString simply removes the hyphens from the Guid.
            string key = _typeToIdentifier.GetOrAdd(type, Guid.NewGuid().ToString("N"));
            _ = _identifierToType.GetOrAdd(key, type);
            return key;
        }

        public Type GetRootComponent(string identifier) =>
            !_identifierToType.TryGetValue(identifier, out var type) ? null : type;
    }

}
