using CH.IoC.Attributes;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestPlugin
{
    [Wire]
    internal class DoSomethingTwo : IDoSomething
    {
        public string DoSomething(string toThis)
        {
            return "TWO: " + toThis;
        }
    }
}