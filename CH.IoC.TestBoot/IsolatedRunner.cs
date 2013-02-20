using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CH.IoC.Infrastructure;
using CH.IoC.TestLog;
using CH.IoC.TestSrv.Interface;

namespace CH.IoC.TestBoot
{
    internal sealed class IsolatedRunner : MarshalByRefObject, IIsolatedRunner
    {
        private CancellationTokenSource _cancellationTokenSource;

        public IsolatedRunner()
        {
            Debug.WriteLine("here");
        }

        async void IIsolatedRunner.Run()
        {
            try
            {
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    IResolver resolver = new Resolver(new[] {"CH.IoC."});
                    var directories = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath.Split(Path.PathSeparator);
                    resolver.LoadDynamicAssemblies(directories);
                    var log = resolver.Resolve<ILog>();
                    log.Log(1, "IsolatedRunner 1");

                    Type isolatedType;
                    try
                    {
                        isolatedType = AppDomain.CurrentDomain.GetAssemblies()
                                                .SelectMany(x => x.GetTypes())
                                                .FirstOrDefault(
                                                    t =>
                                                    !t.IsInterface &&
                                                    t.GetInterface(typeof (IIsolated).FullName) != null);
                    }
                    catch (Exception ex)
                    {
                        log.Log(2, "IsolatedRunner 5: " + ex);
                        return;
                    }
                    if (isolatedType == null)
                    {
                        log.Log(2,
                                "IsolatedRunner 2: Isolated type not found in any of: " +
                                AppDomain.CurrentDomain.SetupInformation.PrivateBinPath);
                        return;
                    }
                    Debug.WriteLine("IsolatedRunner 9 Running: " + isolatedType.AssemblyQualifiedName);
                    var instance = isolatedType.Assembly.CreateInstance(isolatedType.FullName);
                    var isolated = (IIsolated) instance;
                    if (isolated == null)
                    {
                        log.Log(2, "IsolatedRunner 3: Isolated type not found: " + isolatedType.AssemblyQualifiedName);
                        return;
                    }
                    try
                    {
                        Debug.WriteLine("IsolatedRunner 6: Before Run");
                        await isolated.Run(_cancellationTokenSource.Token);
                        Debug.WriteLine("IsolatedRunner 7: After Run");
                    }
                    catch (Exception ex)
                    {
                        log.Log(2, "IsolatedRunner 4: " + ex);
                    }
                    Debug.WriteLine("IsolatedRunner 8: After Run Catch");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("IIsolatedRunner.Run: " + ex);
            }
            _cancellationTokenSource = null;
        }

        void IIsolatedRunner.Cancel()
        {
            var cancellationTokenSource = _cancellationTokenSource;
            if (cancellationTokenSource != null)
                try
                {
                    cancellationTokenSource.Cancel();
                }
// ReSharper disable EmptyGeneralCatchClause
                catch
// ReSharper restore EmptyGeneralCatchClause
                {
                }
        }
    }
}