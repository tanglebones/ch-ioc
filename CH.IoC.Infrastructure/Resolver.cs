using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CH.IoC.Infrastructure
{
    public sealed class Resolver : IResolver
    {
        private readonly IDictionary<string, IList<ComponentInfo>> _components =
            new Dictionary<string, IList<ComponentInfo>>();

        private readonly string[] _assemblyPrefixes;
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private readonly HashSet<string> _loaded = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase); 

        public Resolver(IEnumerable<string> assemblyPrefixes)
        {
            _assemblyPrefixes = assemblyPrefixes.ToArray();
            Setup();
        }

        public Resolver(IEnumerable<string> assemblyPrefixes, IEnumerable<object> overrides)
        {
            _assemblyPrefixes = assemblyPrefixes.ToArray();
            SetupOverrides(overrides);
            Setup();
        }

        private void SetupOverrides(IEnumerable<object> overrides)
        {
            foreach (var instance in overrides)
            {
                var type = instance.GetType();
                foreach (var i in type.GetInterfaces())
                {
                    RegisterType(type, i, instance);
                }
            }
        }

        T IResolver.Resolve<T>()
        {
            var serviceName = typeof(T).AssemblyQualifiedName;
            var o = Resolve(serviceName);

            return (T) o;
        }

        T[] IResolver.ResolveAll<T>()
        {
            var o = ResolveAll(typeof(T).AssemblyQualifiedName);
            if (o == null)
                return new T[] {};
            return ((object[]) o).Cast<T>().ToArray();
        }

        IEnumerable<Tuple<string, IEnumerable<string>>> IResolver.Registered()
        {
            return _components.Select(kvp => Tuple.Create(kvp.Key, kvp.Value.Select(c => c.Name)));
        }

        private object Resolve(string serviceName)
        {
            object o = null;
            IList<ComponentInfo> componentInfos;
            if (_components.TryGetValue(serviceName, out componentInfos))
                o = Instance(componentInfos.First());

            if (o == null)
                throw new Exception("Could not resolve concrete type for interface: " + serviceName);

            return o;
        }

        private object ResolveAll(string serviceName)
        {
            object o = null;
            IList<ComponentInfo> componentInfos;
            if (_components.TryGetValue(serviceName, out componentInfos))
            {
                var a = Array.CreateInstance(componentInfos.First().ServiceType, componentInfos.Count);
                var instances = componentInfos.Select(Instance).ToArray();
                for (var i = 0; i < instances.Length; ++i)
                    a.SetValue(instances[i], i);
                o = a;
            }
            return o;
        }

        private object Instance(ComponentInfo componentInfo)
        {
            if (componentInfo.Instance != null) return componentInfo.Instance;

            foreach (var dependencyInfo in componentInfo.Dependencies)
            {
                if (dependencyInfo.Modifier == DependencyInfo.TypeModifier.None)
                {
                    dependencyInfo.Instance = Resolve(dependencyInfo.ServiceName);
                }
                if (dependencyInfo.Modifier == DependencyInfo.TypeModifier.Array
                    || dependencyInfo.Modifier == DependencyInfo.TypeModifier.Enum)
                {
                    dependencyInfo.Instance = ResolveAll(dependencyInfo.ServiceName);
                }
            }

            var parameters =
                componentInfo
                    .Dependencies
                    .Select(
                        d => d.Instance
                    )
                    .ToArray();

            if (componentInfo.Ctor != null)
            {
                componentInfo.Instance = componentInfo.Ctor.Invoke(parameters);
            }
            else if (componentInfo.MCtor != null)
            {
                componentInfo.Instance = componentInfo.MCtor.Invoke(null, parameters);
            }

            return componentInfo.Instance;
        }

        private void Setup()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _loaded.Add(assembly.FullName);
            }

            var directories = (AppDomain.CurrentDomain.SetupInformation.PrivateBinPath ?? string.Empty).Split(Path.PathSeparator).Concat(new[] { "bin" }).Select(x => Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, x));
            ((IResolver)this).LoadDynamicAssemblies(directories);
            Wire();
        }

        private void Wire()
        {
            var assemblies =
                AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .Where(
                        a =>
                        _assemblyPrefixes
                            .Any(
                                assemblyPrefix =>
                                a
                                    .FullName
                                    .StartsWith(assemblyPrefix)
                            )
                    )
                    .Where(a => NotAlreadySeen(_seen, a)) // remove duplicates, keeper older entries in order.
                    .ToArray()
                ;

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        try
                        {
                            var attrs = type.GetCustomAttributes(true).Where(x=>x.GetType().Name == "Wire").ToArray();
                            foreach (var attr in attrs)
                            {
                                var pi = attr.GetType().GetProperty("InterfaceType");
                                if (pi != null)
                                {
                                    var interfaceType = pi.GetValue(attr, null) as Type;
                                    if (interfaceType != null)
                                    {
                                        RegisterType(type, interfaceType);
                                        continue;
                                    }
                                }
                                foreach (var i in type.GetInterfaces())
                                {
                                    RegisterType(type, i);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch 
                {}
            }
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        try
                        {
                            var attrs = type.GetCustomAttributes(true).Where(x => x.GetType().Name == "Wirer").ToArray();
                            if (attrs.Length <= 0) continue;

                            var mi = type.GetMethod("Wire");
                            if (mi != null && mi.IsStatic)
                            {
                                RegisterWired(type, mi);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private static bool NotAlreadySeen(HashSet<string> seen, Assembly a)
        {
            if (seen.Contains(a.FullName)) return false;
            seen.Add(a.FullName);
            return true;
        }

        private void RegisterWired(Type type, MethodInfo mi)
        {
            var name = type.AssemblyQualifiedName;
            var interfaceType = mi.ReturnType;
            if (!_components.ContainsKey(interfaceType.AssemblyQualifiedName))
            {
                _components[interfaceType.AssemblyQualifiedName] = new List<ComponentInfo>();
            }
            var componentInfos = _components[interfaceType.AssemblyQualifiedName];
            if (componentInfos.Any(x => x.MCtor == mi && x.Type == type)) 
                return;
            var componentInfo = new ComponentInfo
                {
                    Name = name,
                    Type = type,
                    ServiceType = interfaceType,
                    MCtor = mi,
                    Dependencies = mi
                        .GetParameters()
                        .Select(DependencyInfoFromParameterInfo)
                        .ToArray()
                };

            componentInfos.Add(componentInfo);
        }

        private void RegisterType(Type type, Type interfaceType, object instance=null)
        {
            var name = type.AssemblyQualifiedName;
            if (!_components.ContainsKey(interfaceType.AssemblyQualifiedName))
            {
                _components[interfaceType.AssemblyQualifiedName] = new List<ComponentInfo>();
            }
            var componentInfos = _components[interfaceType.AssemblyQualifiedName];
            if (componentInfos.Any(x => x.ServiceType == interfaceType && x.Type == type))
                return;
            var componentInfo = new ComponentInfo {Name = name, Type = type, ServiceType = interfaceType, Instance = instance};
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
                    .Select(DependencyInfoFromParameterInfo)
                    .ToArray();

            componentInfos.Add(componentInfo);
        }

        private static DependencyInfo DependencyInfoFromParameterInfo(ParameterInfo x)
        {
            if (x.ParameterType.Name == "IEnumerable`1")
            {
                return new DependencyInfo
                    {
                        Modifier = DependencyInfo.TypeModifier.Enum,
                        ServiceName = x.ParameterType.GetGenericArguments().First().AssemblyQualifiedName
                    };
            }
            if (x.ParameterType.IsArray)
            {
                return new DependencyInfo
                    {
                        Modifier = DependencyInfo.TypeModifier.Array,
                        ServiceName = x.ParameterType.GetElementType().AssemblyQualifiedName
                    };
            }
            return new DependencyInfo
                {
                    Modifier = DependencyInfo.TypeModifier.None,
                    ServiceName = x.ParameterType.AssemblyQualifiedName
                };
        }

        void IResolver.LoadDynamicAssemblies(IEnumerable<string> directories)
        {
            var dlls =
                directories
                    .Where(Directory.Exists)
                    .SelectMany(path => Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
                    .Where(d =>
                        {
                            var a = (Path.GetFileNameWithoutExtension(d) ?? string.Empty);
                            return _assemblyPrefixes.Any(
                                p => a.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)
                                );
                        })
                    ;

            LoadDlls(dlls);
            Wire();
        }

        [ExcludeFromCodeCoverage]
        private void LoadDlls(IEnumerable<string> dlls)
        {
            foreach (var dll in dlls)
                try
                {
                    var fullpath = new FileInfo(dll).FullName;
                    Assembly ra;
                    try
                    {
                        ra = Assembly.ReflectionOnlyLoadFrom(fullpath);
                    }
                    catch
                    {
                        continue;
                    }
                    if (_loaded.Contains(ra.FullName)) continue;

                    var a = Assembly.Load(ra.GetName());
                    _loaded.Add(a.FullName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("could not load assembly \"" + dll + "\": " + ex);
                }
        }

        private class ComponentInfo
        {
            public ConstructorInfo Ctor;
            public IEnumerable<DependencyInfo> Dependencies;
            public object Instance;
            public MethodInfo MCtor;
            public string Name;
            public Type ServiceType;
            public Type @Type;
        }

        private class DependencyInfo
        {
            public enum TypeModifier
            {
                None,
                Array,
                Enum
            };

            public object Instance;
            public TypeModifier Modifier;
            public string ServiceName;
        }
    }
}