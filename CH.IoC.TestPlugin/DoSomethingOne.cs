using CH.IoC.Infrastructure.Wiring;
using CH.IoC.TestPlugin.Interface;

namespace CH.IoC.TestPlugin
{
    [Wire]
    internal sealed class DoSomethingOne : IDoSomething
    {
        private readonly IOnePrefix _onePrefix;

        public DoSomethingOne(IOnePrefix onePrefix)
        {
            _onePrefix = onePrefix;
        }

        public string DoSomething(string toThis)
        {
            return _onePrefix.Prefix + ": " + toThis;
        }
    }

    internal interface IOnePrefix
    {
        string Prefix { get; }
    }

    [Wire]
    sealed class OnePrefix :IOnePrefix
    {
        string IOnePrefix.Prefix
        {
            get { return "ONE";  }
        }
    }
}