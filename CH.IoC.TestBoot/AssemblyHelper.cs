using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CH.IoC.TestBoot
{
    internal static class AssemblyHelper
    {
        public static void Setup()
        {
            ClearEventInvocations(AppDomain.CurrentDomain, "_AssemblyResolve");
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private static void ClearEventInvocations(object obj, string eventName)
        {
            var fi = GetEventField(obj.GetType(), eventName);
            if (fi == null) return;
            fi.SetValue(obj, null);
        }

        private static FieldInfo GetEventField(Type type, string eventName)
        {
            FieldInfo field = null;
            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null &&
                    (field.FieldType == typeof (MulticastDelegate) ||
                     field.FieldType.IsSubclassOf(typeof (MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("EVENT_" + eventName.ToUpper(),
                                      BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }
            return field;
        }


        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                Debug.WriteLine("AssemblyResolver: " + args.Name);
                {
                    // WAT?
                    var assemblies =
                        AppDomain
                            .CurrentDomain
                            .GetAssemblies()
                            .Where(
                                a =>
                                    {
                                        try
                                        {
                                            return a.FullName
                                                    .Equals(
                                                        args.Name,
                                                        StringComparison.InvariantCultureIgnoreCase
                                                );
                                        }
                                        catch
                                        {
                                            return false;
                                        }
                                    }
                            ).ToArray();
                    if (assemblies.Any())
                    {
                        Debug.WriteLine("AssemblyResolver assembly already loaded: " + args.Name);
                        return assemblies.First();
                    }
                }

                var parts = args.Name.Split(',');
                var locations =
                    (AppDomain.CurrentDomain.SetupInformation.PrivateBinPath ?? string.Empty)
                        .Split(Path.PathSeparator)
                        .Concat(new[] {"bin"})
                        .Select(x => Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, x))
                    ;

                foreach (var location in locations)
                {
                    var file = location + "\\" + parts[0].Trim() + ".DLL";
                    if (!File.Exists(file))
                        continue;

                    Assembly ra;
                    try
                    {
                        ra = Assembly.ReflectionOnlyLoadFrom(file);
                    }
                    catch
                    {
                        continue;
                    }

                    if (ra.FullName != args.Name)
                        continue;

                    try
                    {
                        var a = Assembly.LoadFrom(file);
                        Debug.WriteLine("AssemblyResolver loaded " + args.Name + " from " + location);
                        return a;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AssemblyResolver: " + ex);
            }
            return null;
        }

    }
}