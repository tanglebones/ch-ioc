using System.Collections.Generic;
using System.Linq;
using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestHost.Interface;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestHost
{
    [Wire]
    internal sealed class TestHost : ITestHost
    {
        private readonly IDoSomething[] _plugins;

        public TestHost(IDoSomething[] plugins)
        {
            _plugins = plugins;
        }

        public IEnumerable<string> Run(string toWhat)
        {
            return _plugins.Select(x => x.DoSomething(toWhat));
        }
    }
}