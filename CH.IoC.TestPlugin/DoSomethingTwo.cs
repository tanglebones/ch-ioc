using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestPlugin
{
    [Wire]
    internal sealed class DoSomethingTwo : IDoSomething
    {
        public string DoSomething(string toThis)
        {
            return "TWO: " + toThis;
        }
    }
}