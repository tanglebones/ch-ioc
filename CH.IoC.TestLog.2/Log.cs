using System;
using System.Diagnostics;
using System.Globalization;
using CH.IoC.Infrastructure.Wiring;

namespace CH.IoC.TestLog
{
    [Wire]
    internal sealed class Log : ILog
    {
        void ILog.Log(int level, Func<string> message)
        {
            Write(level.ToString(CultureInfo.InvariantCulture) + " " + message());
        }

        private static void Write(string s)
        {
            Debug.WriteLine(s);
        }
    }
}