using System.Collections.Generic;

namespace CH.IoC.TestHost.Interface
{
    public interface ITestHost
    {
        IEnumerable<string> Run(string toWhat);
    }
    public interface ITestHostArr : ITestHost { }
    public interface ITestHostEnum : ITestHost { }
}