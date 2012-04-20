using CH.IoC.Attributes;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestPlugin
{
    [Wire]
    internal class DoSomethingOne : IDoSomething
    {
        public string DoSomething(string toThis)
        {
            return "ONE: " + toThis;
        }
    }
}