using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace Microsoft.AspNetCore.Components
{
    internal class RootComponentTypeCache
    {
        public string RegisterRootComponent(Type type)
        {
            var key = JsonSerializer.Serialize(new Key(type.Assembly.GetName().Name, type.FullName));
            return key;
        }

        public Type GetRootComponent(string identifier)
        {
            var key = JsonSerializer.Deserialize<Key>(identifier);
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == key.Assembly);
            if (assembly == null)
            {
                return null;
            }
            var type = assembly.GetType(key.Type, throwOnError: false, ignoreCase: false);
            return type;
        }

        private struct Key
        {
            public Key(string assembly, string type) : this()
            {
                Assembly = assembly;
                Type = type;
            }

            public string Assembly { get; set; }
            public string Type { get; set; }
        }
    }
}
