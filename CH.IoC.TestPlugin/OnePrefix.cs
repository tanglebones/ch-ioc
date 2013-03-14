using CH.IoC.Infrastructure.Wiring;

namespace CH.IoC.TestPlugin
{
    [Wire]
    sealed class OnePrefix :IOnePrefix
    {
        string IOnePrefix.Prefix
        {
            get { return "ONE";  }
        }
    }
}