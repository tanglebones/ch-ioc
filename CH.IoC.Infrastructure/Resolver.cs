using System;
using Castle.Windsor;

namespace CH.IoC.Intrasturcture
{
    public sealed class Resolver : IResolve, IDisposable
    {
        private readonly IWindsorContainer _container;

        public Resolver(string assemblyPrefix, string log4NetConfigFileName = null)
        {
            _container = Windsor.Container(assemblyPrefix, log4NetConfigFileName);
        }

        public T Resolve<T>()
        {
            return _container.Resolve<T>();
        }

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}