using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CH.IoC.Infrastructure.Wiring;

namespace CH.IoC.Infrastructure
{
    public sealed class Resolver : IResolver
    {
        private readonly IDictionary<string, IList<ComponentInfo>> _components =
            new Dictionary<string, IList<ComponentInfo>>();

        public Resolver(string assemblyPrefix)
        {
            Setup(new[] {assemblyPrefix});
        }

        public Resolver(IEnumerable<string> assemblyPrefixes)
        {
            Setup(assemblyPrefixes);
        }

        T IResolver.Resolve<T>()
        {
            return (T) Resolve(typeof (T).FullName);
        }

        T[] IResolver.ResolveAll<T>()
        {
            var o = ResolveAll(typeof (T).FullName);
            if (o == null)
                return new T[] {};
            return ((object[]) o).Cast<T>().ToArray();
        }

        public IEnumerable<Tuple<string, IEnumerable<string>>> Registered()
        {
            return _components.Select(kvp => Tuple.Create(kvp.Key, kvp.Value.Select(c => c.Name)));
        }

        private object Resolve(string serviceName)
        {
            object o = null;
            IList<ComponentInfo> componentInfos;
            if (_components.TryGetValue(serviceName, out componentInfos))
                o = Instance(componentInfos.First());
            return o;
        }

        private object ResolveAll(string serviceName)
        {
            object o = null;
            IList<ComponentInfo> componentInfos;
            if (_components.TryGetValue(serviceName, out componentInfos))
            {
                var a = Array.CreateInstance(componentInfos.First().ServiceType,componentInfos.Count);
                var instances = componentInfos.Select(Instance).ToArray();
                for (var i = 0; i < instances.Length; ++i)
                    a.SetValue(instances[i],i);
                o = a;
            }
            return o;
        }

        private object Instance(ComponentInfo componentInfo)
        {
            if (componentInfo == null) return null;
            if (componentInfo.Instance != null) return componentInfo.Instance;

            foreach (var dependencyInfo in componentInfo.Dependencies)
            {
                if (dependencyInfo.Instance != null) continue;

                if (dependencyInfo.Modifier == DependencyInfo.TypeModifier.None)
                {
                    dependencyInfo.Instance = Resolve(dependencyInfo.ServiceName);
                }
                if (dependencyInfo.Modifier == DependencyInfo.TypeModifier.Array)
                {
                    dependencyInfo.Instance = ResolveAll(dependencyInfo.ServiceName);
                }
            }

            var parameters = 
                componentInfo
                .Dependencies
                .Select(
                    d =>d.Instance
                )
                .ToArray();
            componentInfo.Instance = componentInfo.Ctor.Invoke(parameters);
            return componentInfo.Instance;
        }

        private void Setup(IEnumerable<string> assemblyPrefixes)
        {
            var prefixesAsArray = assemblyPrefixes.ToArray();
            foreach (var assemblyPrefix in prefixesAsArray)
                LoadDynamicAssemblies(assemblyPrefix);

            var assemblies = BuildAssemblyDictionary(prefixesAsArray);

            WireByAttribute(assemblies);
        }

        private static IDictionary<string, Assembly> BuildAssemblyDictionary(IEnumerable<string> assemblyPrefixes)
        {
            var assemblies = new Dictionary<string, Assembly>();
            foreach (
                var a in
                    AppDomain.CurrentDomain.GetAssemblies()
                             .Where(a => assemblyPrefixes.Any(assemblyPrefix => a.FullName.StartsWith(assemblyPrefix))))
                assemblies[a.FullName] = a;
            return assemblies;
        }

        private void WireByAttribute(IDictionary<string, Assembly> assemblies)
        {
            foreach (var assembly in assemblies.Values)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var attrs = type.GetCustomAttributes(typeof (Wire), true);
                    foreach (var interfaceType in from Wire attr in attrs select attr.InterfaceType)
                    {
                        if (interfaceType == null)
                        {
                            foreach (var i in type.GetInterfaces())
                            {
                                Register(type, i);
                            }
                        }
                        else
                        {
                            Register(type, interfaceType);
                        }
                    }
                }
            }
        }

        private void Register(Type type, Type interfaceType)
        {
            var name = type.FullName + "^as^" + interfaceType.FullName;
            if (!_components.ContainsKey(interfaceType.FullName))
            {
                _components[interfaceType.FullName] = new List<ComponentInfo>();
            }
            var componentInfos = _components[interfaceType.FullName];
            var componentInfo = new ComponentInfo {Name = name, Type = type, ServiceType = interfaceType};
            var ctor =
                type
                    .GetConstructors()
                    .Where(
                        x => x
                                 .GetParameters()
                                 .All(
                                     p =>
                                         {
                                             var pi = p.ParameterType;
                                             return pi.IsInterface ||
                                                    (pi.IsArray &&
                                                     pi.GetElementType()
                                                       .IsInterface);
                                         }
                                 )
                    )
                    .OrderByDescending(x => x.GetParameters().Length)
                    .First();
            componentInfo.Ctor = ctor;
            componentInfo.Dependencies =
                ctor
                    .GetParameters()
                    .Select(
                        x =>
                            {
                                if (x.ParameterType.IsArray)
                                {
                                    return new DependencyInfo
                                        {
                                            Modifier = DependencyInfo.TypeModifier.Array,
                                            ServiceName = x.ParameterType.GetElementType().FullName
                                        };
                                }
                                return new DependencyInfo
                                    {
                                        Modifier = DependencyInfo.TypeModifier.None,
                                        ServiceName = x.ParameterType.FullName
                                    };
                            }
                    ).ToArray();

            componentInfos.Add(componentInfo);
        }

        private static void LoadDynamicAssemblies(string assemblyPrefix)
        {
            foreach (var dll in
                new[]
                    {
                        AppDomain.CurrentDomain.BaseDirectory,
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin")
                    }
                    .Where(Directory.Exists)
                    .SelectMany(path => Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
                )
                try
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(dll);
                    if (fileNameWithoutExtension == null) continue;
                    if (!fileNameWithoutExtension.StartsWith(assemblyPrefix)) continue;
                    Assembly.Load(new AssemblyName {CodeBase = new FileInfo(dll).FullName});
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("could not load assembly \"" + dll + "\": " + ex);
                }
        }

        private class ComponentInfo
        {
            public IEnumerable<DependencyInfo> Dependencies;
            public object Instance;
            public string Name;
            public Type @Type;
            public ConstructorInfo Ctor;
            public Type ServiceType;
        }

        private class DependencyInfo
        {
            public enum TypeModifier
            {
                None,
                Array,
            };

            public object Instance;
            public TypeModifier Modifier;
            public string ServiceName;
        }
    }
}