using System;
using System.Threading.Tasks;
using CH.IoC.Infrastructure;
using CH.IoC.TestLog;
using CH.IoC.TestSrv.Interface;

namespace CH.IoC.TestIsolated
{
    internal sealed class Isolated : MarshalByRefObject, IIsolated
    {
        Task IIsolated.Run(System.Threading.CancellationToken token)
        {
            IResolver resolver = new Resolver(new[] {"CH.IoC."});
            var log = resolver.Resolve<ILog>();
            log.Log(1, "Isolated 4.1.0.0");
            return Task.Delay(1, token);
        }
    }
}