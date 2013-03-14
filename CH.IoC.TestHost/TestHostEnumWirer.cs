using System.Collections.Generic;
using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestHost.Interface;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestHost
{
    [Wirer]
    internal sealed class TestHostEnumWirer
    {
        public static ITestHostEnum Wire(IEnumerable<IDoSomething> plugins)
        {
            return new TestHostEnum(plugins);
        }
    }
}