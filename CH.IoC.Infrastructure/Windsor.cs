using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using CH.IoC.Infrastructure.Wiring;
using Castle.Core.Logging;
using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Castle.Windsor.Installer;

namespace CH.IoC.Intrasturcture
{
    internal static class Windsor
    {
        internal static IWindsorContainer Container(string assemblyPrefix, string log4NetConfigFileName = null)
        {
            var container = SetupContainer(log4NetConfigFileName);
            var logger = container.Resolve<ILogger>() ?? new NullLogger();

            LoadDynamicAssemblies(assemblyPrefix, logger);

            var assemblies = BuildAssemblyDictionary(assemblyPrefix);

            WireByAttribute(container, assemblies);
            WireByInstaller(logger, container, assemblies);

            return container;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private static WindsorContainer SetupContainer(string log4NetConfigFileName)
        {
            var container = new WindsorContainer();

            container.Kernel.Resolver.AddSubResolver(new CollectionResolver(container.Kernel));
            if (!String.IsNullOrEmpty(log4NetConfigFileName))
                container.AddFacility(new LoggingFacility(LoggerImplementation.Log4net, log4NetConfigFileName));
            else
            {
                container.Register(Component.For<ILogger>().Instance(NullLogger.Instance));
            }
            return container;
        }

        private static IDictionary<string, Assembly> BuildAssemblyDictionary(string assemblyPrefix)
        {
            var assemblies = new Dictionary<string, Assembly>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(assemblyPrefix)))
                assemblies[a.FullName] = a;
            return assemblies;
        }

        private static void WireByInstaller(ILogger logger, IWindsorContainer container,
                                            IDictionary<string, Assembly> assemblies)
        {
            foreach (var assembly in assemblies.Values)
            {
                try
                {
                    container.Install(FromAssembly.Instance(assembly));
                }
                catch (Exception ex)
                {
                    logger.Warn("IoC install failed for assembly \"" + assembly.FullName + "\": " + ex);
                }
            }
        }

        private static void WireByAttribute(IWindsorContainer container, IDictionary<string, Assembly> assemblies)
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
                                container.Register(
                                    Component.For(i)
                                        .ImplementedBy(type)
                                        .Named(type.FullName + "^as^" + i.FullName)
                                    );
                            }
                        }
                        else
                        {
                            container.Register(
                                Component.For(interfaceType)
                                    .ImplementedBy(type)
                                    .Named(type.FullName + "^as^" + interfaceType.FullName));
                        }
                    }
                }
            }
        }

        private static void LoadDynamicAssemblies(string assemblyPrefix, ILogger logger)
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
                    Assembly.Load(new AssemblyName(fileNameWithoutExtension));
                }
                catch (Exception ex)
                {
                    logger.Warn("could not load assembly \"" + dll + "\": " + ex);
                }
        }
    }
}