using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            Setup(new[] {assemblyPrefix}, Enumerable.Empty<string>());
        }

        public Resolver(IEnumerable<string> assemblyPrefixes)
        {
            Setup(assemblyPrefixes, Enumerable.Empty<string>());
        }

        public Resolver(IEnumerable<string> assemblyPrefixes, IEnumerable<string> excludePrefixes)
        {
            Setup(assemblyPrefixes, excludePrefixes);
        }

        T IResolver.Resolve<T>()
        {
            var serviceName = typeof (T).FullName;
            var o = Resolve(serviceName);

            return (T) o;
        }

        T[] IResolver.ResolveAll<T>()
        {
            var o = ResolveAll(typeof (T).FullName);
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

        private void Setup(IEnumerable<string> includePrefixes, IEnumerable<string> excludePrefixes)
        {
            IEnumerable<string> directories = (AppDomain.CurrentDomain.SetupInformation.PrivateBinPath ?? string.Empty).Split(Path.PathSeparator).Concat(new[] { "bin" }).Select(x => Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, x));
            ((IResolver)this).LoadDynamicAssemblies(includePrefixes, excludePrefixes, directories);
            Wire(includePrefixes);
        }

        private void Wire(IEnumerable<string> assemblyPrefixes)
        {
            var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var assemblies =
                AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .Where(
                        a =>
                        assemblyPrefixes
                            .Any(
                                assemblyPrefix =>
                                a
                                    .FullName
                                    .StartsWith(assemblyPrefix)
                            )
                    )
                    .Where(a => NotAlreadySeen(seen, a)) // remove duplicates, keeper older entries in order.
                    .ToArray()
                ;

            foreach (var assembly in assemblies)
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
                                RegisterType(type, i);
                            }
                        }
                        else
                        {
                            RegisterType(type, interfaceType);
                        }
                    }
                }
            }
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var attrs = type.GetCustomAttributes(typeof (Wirer), true);
                    if (attrs.Length > 0)
                    {
                        var mi = type.GetMethod("Wire");
                        if (mi != null && mi.IsStatic)
                        {
                            RegisterWired(type, mi);
                        }
                    }
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
            var name = type.FullName;
            var interfaceType = mi.ReturnType;
            if (!_components.ContainsKey(interfaceType.FullName))
            {
                _components[interfaceType.FullName] = new List<ComponentInfo>();
            }
            var componentInfos = _components[interfaceType.FullName];
            var componentInfo = new ComponentInfo {Name = name, Type = type, ServiceType = interfaceType};
            componentInfo.MCtor = mi;
            componentInfo.Dependencies =
                mi
                    .GetParameters()
                    .Select(DependencyInfoFromParameterInfo)
                    .ToArray();

            componentInfos.Add(componentInfo);
        }

        private void RegisterType(Type type, Type interfaceType)
        {
            var name = type.FullName;
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
                        ServiceName = x.ParameterType.GetGenericArguments().First().FullName
                    };
            }
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

        void IResolver.LoadDynamicAssemblies(IEnumerable<string> includePrefixes, IEnumerable<string> excludePrefixes, IEnumerable<string> directories)
        {
            var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var dlls =
                directories
                    .Where(Directory.Exists)
                    .SelectMany(path => Directory.EnumerateFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
                    .Where(d =>
                        {
                            var a = (Path.GetFileNameWithoutExtension(d) ?? string.Empty);
                            if (!includePrefixes.Any(
                                    p => a.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)
                                    )
                                )
                                return false;

                            if (seen.Contains(a)) return false;
                            seen.Add(a);
                            if (excludePrefixes.Any(p => a.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                                return false;

                            return true;
                        })
                    .Select(
                        d =>
                        Tuple.Create(d, Assembly.ReflectionOnlyLoadFrom(d).GetReferencedAssemblies().Select(a=>a.Name.ToLowerInvariant()).ToArray(), Path.GetFileNameWithoutExtension(d).ToLowerInvariant())
                    )
                    .OrderBy(t => t, new Comp<Tuple<string,string[],string>> (
                        (a1, a2) =>
                            {
                                if (a1.Item2.Contains(a2.Item3)) return 1;
                                if (a2.Item2.Contains(a1.Item3)) return - 1;
                                return 0;
                            }))
                    .Select(t => t.Item1)
                    .ToArray();

            LoadDlls(dlls);
        }

        [ExcludeFromCodeCoverage]
        private void LoadDlls(string[] dlls)
        {
            foreach (var dll in dlls)
                try
                {
                    Assembly.Load(new AssemblyName {CodeBase = new FileInfo(dll).FullName});
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("could not load assembly \"" + dll + "\": " + ex);
                    throw;
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

    internal class Comp<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _func;

        public Comp(Func<T, T, int> func)
        {
            _func = func;
        }

        public int Compare(T x, T y)
        {
            return _func(x, y);
        }
    }

}