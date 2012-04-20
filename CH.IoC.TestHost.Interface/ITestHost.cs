using System.Collections.Generic;

namespace CH.IoC.TestHost.Interface
{
    public interface ITestHost
    {
        IEnumerable<string> Run(string toWhat);
    }
}