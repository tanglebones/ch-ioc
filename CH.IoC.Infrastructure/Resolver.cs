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
        private readonly string[] _assemblyPrefixes;

        private readonly IDictionary<string, IList<ComponentInfo>> _components =
            new Dictionary<string, IList<ComponentInfo>>();

        private readonly HashSet<string> _loaded = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        private bool _setup;
        private Action<Exception> _wireExceptionAction;

        public Resolver(IEnumerable<string> assemblyPrefixes)
        {
            _assemblyPrefixes = assemblyPrefixes.ToArray();
        }

        public Resolver(IEnumerable<string> assemblyPrefixes, IEnumerable<object> overrides)
        {
            _assemblyPrefixes = assemblyPrefixes.ToArray();
            SetupOverrides(overrides);
        }

        T IResolver.Resolve<T>()
        {
            Setup();
            var serviceName = typeof (T).AssemblyQualifiedName;
            var o = Resolve(serviceName);

            return (T) o;
        }

        T[] IResolver.ResolveAll<T>()
        {
            Setup();
            var o = ResolveAll(typeof (T).AssemblyQualifiedName);
            if (o == null)
                return new T[] {};
            return ((object[]) o).Cast<T>().ToArray();
        }

        IEnumerable<Tuple<string, IEnumerable<string>>> IResolver.Registered()
        {
            Setup();
            return _components.Select(kvp => Tuple.Create(kvp.Key, kvp.Value.Select(c => c.Name)));
        }

        void IResolver.LoadDynamicAssemblies(IEnumerable<string> directories)
        {
            Setup();
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

        public Resolver OnWireException(Action<Exception> action)
        {
            _wireExceptionAction = action;
            return this;
        }

        private void SetupOverrides(IEnumerable<object> overrides)
        {
            foreach (var instance in overrides)
            {
                var type = instance.GetType();
                foreach (var i in type.GetInterfaces())
                {
                    RegisterType(type, i, new[] {instance});
                }
            }
        }

        private object Resolve(string serviceName)
        {
            object o = null;
            IList<ComponentInfo> componentInfos;
            if (_components.TryGetValue(serviceName, out componentInfos))
            {
                var first = componentInfos.FirstOrDefault();
                if (first != null)
                {
                    var os = Instances(first);
                    o = os.FirstOrDefault();
                }
            }

            if (o == null)

                throw new Exception("Could not resolve concrete type for interface: " + serviceName);

            return o;
        }

        private object ResolveAll(string serviceName)
        {
            IList<ComponentInfo> componentInfos;
            if (!_components.TryGetValue(serviceName, out componentInfos)) return null;
            var instances = componentInfos.Select(Instances).SelectMany(x => x).ToArray();
            var a = Array.CreateInstance(componentInfos.First().ServiceType, instances.Length);
            for (var i = 0; i < instances.Length; ++i)
                a.SetValue(instances[i], i);
            return a;
        }

        private IEnumerable<object> Instances(ComponentInfo componentInfo)
        {
            if (componentInfo.Initialized)
                return componentInfo.Instances;

            var existingInstancesOfThisConcreteType =
                _components.Values.SelectMany(i => i).Where(x => x.Initialized)
                    .SelectMany(x => x.Instances)
                    .Where(y =>
                    {
                        var type = y.GetType();
                        return type.FullName == componentInfo.Type.FullName;
                    }
                    )
                    .ToArray();

            if (existingInstancesOfThisConcreteType.Any())
            {
                // don't create more than one instance of the concrete type
                componentInfo.Instances = existingInstancesOfThisConcreteType;
                componentInfo.Initialized = true;
                return existingInstancesOfThisConcreteType;
            }

            if (componentInfo.Dependencies == null)
            {
                throw new Exception("Can't determine component dependencies. Check that " + componentInfo.Type.Name + " has a public ctor.");
            }
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

            var newObjs = new List<object>();
            if (componentInfo.Ctor != null)
            {
                var obj = componentInfo.Ctor.Invoke(parameters);
                newObjs.Add(obj);
                componentInfo.Instances.Add(obj);
            }
            else if (componentInfo.MCtor != null)
            {
                var temp = componentInfo.MCtor.Invoke(null, parameters);
                switch (componentInfo.RType)
                {
                    case DependencyInfo.TypeModifier.None:
                        componentInfo.Instances.Add(temp);
                        newObjs.Add(temp);
                        break;
                    case DependencyInfo.TypeModifier.Enum:
                        foreach (var obj in (IEnumerable<object>) temp)
                        {
                            componentInfo.Instances.Add(obj);
                            newObjs.Add(obj);
                        }
                        break;
                    case DependencyInfo.TypeModifier.Array:
                        foreach (var obj in (object[]) temp)
                        {
                            componentInfo.Instances.Add(obj);
                            newObjs.Add(obj);
                        }
                        break;
                }
            }

            if (newObjs.Count > 0)
            {
                foreach (var obj in newObjs)
                    foreach (var iface in 
                        obj
                            .GetType()
                            .GetInterfaces()
                            .Where(x => x != componentInfo.ServiceType))
                    {
                        IList<ComponentInfo> ci;
                        if (!_components.TryGetValue(iface.AssemblyQualifiedName, out ci)) continue;
                        var obj1 = obj;
                        foreach (var c in ci.Where(c => !c.Instances.Contains(obj1)))
                        {
                            c.Instances.Add(obj);
                        }
                    }
            }
            componentInfo.Initialized = true;
            return componentInfo.Instances;
        }

        private void Setup()
        {
            if (_setup) return;
            _setup = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _loaded.Add(assembly.FullName);
            }

            var directories =
                (AppDomain.CurrentDomain.SetupInformation.PrivateBinPath ?? string.Empty)
                    .Split(Path.PathSeparator)
                    .Concat(new[] {"bin"})
                    .Select(
                        x =>
                            Path.Combine(
                                AppDomain.CurrentDomain
                                    .SetupInformation
                                    .ApplicationBase,
                                x));
            ((IResolver) this).LoadDynamicAssemblies(directories);
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
                            var attrs = type.GetCustomAttributes(true).Where(x => x.GetType().Name == "Wire").ToArray();
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
                        catch (Exception ex)
                        {
                            _wireExceptionAction(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _wireExceptionAction(ex);
                }
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
                        catch (Exception ex)
                        {
                            _wireExceptionAction(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _wireExceptionAction(ex);
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
            var rtype = DependencyInfo.TypeModifier.None;
            if (interfaceType.IsGenericType)
            {
                if (interfaceType.GetGenericTypeDefinition().Name == "IEnumerable`1")
                {
                    interfaceType = interfaceType.GetGenericArguments().First();
                    rtype = DependencyInfo.TypeModifier.Enum;
                }
            }
            else if (interfaceType.IsArray)
            {
                interfaceType = interfaceType.GetElementType();
                rtype = DependencyInfo.TypeModifier.Array;
            }
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
                RType = rtype,
                Dependencies = mi
                    .GetParameters()
                    .Select(DependencyInfoFromParameterInfo)
                    .ToArray()
            };

            componentInfos.Add(componentInfo);
        }

        private void RegisterType(Type type, Type interfaceType, IList<object> instances = null)
        {
            try
            {
                var name = type.AssemblyQualifiedName;
                var interfaceName = interfaceType.AssemblyQualifiedName ?? interfaceType.Name;

                if (!_components.ContainsKey(interfaceName))
                {
                    _components[interfaceName] = new List<ComponentInfo>();
                }

                var componentInfos = _components[interfaceName];
                if (componentInfos.Any(x => x.ServiceType == interfaceType && x.Type == type))
                    return;
                var componentInfo = new ComponentInfo
                {
                    Name = name,
                    Type = type,
                    ServiceType = interfaceType,
                };

                if (instances != null)
                {
                    componentInfo.Instances = instances;
                    componentInfo.Initialized = true;
                }
                var ctor = type
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
                    .FirstOrDefault();

                componentInfo.Ctor = ctor;
                if (ctor != null)
                    componentInfo.Dependencies =
                        ctor
                            .GetParameters()
                            .Select(DependencyInfoFromParameterInfo)
                            .ToArray();

                componentInfos.Add(componentInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("- Component name={0} type={1} ServiceType={2}\n{3}", 
                    type.AssemblyQualifiedName, 
                    type.FullName,
                    interfaceType.FullName,
                    ex);
                throw;
            }
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
            public bool Initialized;
            public IList<object> Instances = new List<object>();
            public MethodInfo MCtor;
            public string Name;
            public DependencyInfo.TypeModifier RType;
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