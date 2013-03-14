using System;
using System.Runtime.Remoting;

namespace CH.IoC.TestBoot
{
    internal interface IIsolatedRunner
    {
        void Run();
        void Cancel();
        object GetLifetimeService();
        object InitializeLifetimeService();
        ObjRef CreateObjRef(Type requestedType);
    }
}