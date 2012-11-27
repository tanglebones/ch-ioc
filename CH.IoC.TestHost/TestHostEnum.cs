using System.Collections.Generic;
using System.Linq;
using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestHost.Interface;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestHost
{
    [Wire]
    internal sealed class TestHostEnum : ITestHostEnum
    {
        private readonly IEnumerable<IDoSomething> _plugins;

        public TestHostEnum(IEnumerable<IDoSomething> plugins)
        {
            _plugins = plugins;
        }

        public IEnumerable<string> Run(string toWhat)
        {
            return _plugins.Select(x => x.DoSomething(toWhat));
        }
    }
}