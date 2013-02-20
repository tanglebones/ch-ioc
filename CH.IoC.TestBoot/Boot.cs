using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CH.IoC.Infrastructure;
using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestLog;
using CH.IoC.TestSrv.Interface;

namespace CH.IoC.TestBoot
{
    [Wire]
    internal sealed class Boot : IBoot
    {
        private ILog _log;

        async Task IBoot.Run(CancellationToken token)
        {
            IResolver resolver = new Resolver(new[] {"CH.IoC."});
            _log = resolver.Resolve<ILog>();
            _log.Log(1, "Boot 1");

            var tasks =
                new[]
                    {
                        "isolated\\0",
                        "isolated\\1",
                        "isolated\\2"
                    }
                .Select(path => Run(path, token))
                .ToArray(); // force start

            foreach (var task in tasks)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    _log.Log(2, "Boot 2: " + ex);
                }
            }

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10, token);
            }
        }

        private async Task Run(string directory, CancellationToken token)
        {
            var applicationBase = Environment.CurrentDirectory;
            var abPrefix = applicationBase.Length + 1;
            var relBoot = Path.GetDirectoryName(Path.GetFullPath(typeof(Boot).Assembly.Location).Substring(abPrefix));
            var relDir = Path.GetFullPath(directory).Substring(abPrefix);

            if (!token.IsCancellationRequested)
            {
                // ReSharper disable AccessToDisposedClosure
                var isolated = new Isolate<IsolatedRunner, IIsolatedRunner>(null, new AppDomainSetup { ApplicationBase = applicationBase, PrivateBinPath = relDir + Path.PathSeparator + relBoot });
                using(isolated)
                {
                    var reg = token.Register(() => isolated.Value.Cancel());using (reg)
                    try
                    {
                        Debug.WriteLine("Start " + directory);
                        await Task.Factory.StartNew(() => isolated.Value.Run(), token);
                    }
                    catch (TaskCanceledException)
                    {
                        
                    }
                    catch (Exception ex)
                    {
                        _log.Log(2, "Boot Run: " + ex.ToString());
                    }
                    Debug.WriteLine("Finish " + directory);
                }
                // ReSharper restore AccessToDisposedClosure
            }
        }

    }
}
