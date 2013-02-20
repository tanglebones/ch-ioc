using System.Diagnostics;
using CH.IoC.Infrastructure.Wiring;

namespace CH.IoC.TestLog
{
    [Wire]
    internal sealed class Log : ILog
    {
        void ILog.Log(string message)
        {
            Write(message);
        }

        private static void Write(string s)
        {
            Debug.WriteLine(s);
        }
    }
}