using System;
using System.Collections.Generic;
using Castle.Windsor;

namespace CH.IoC.Infrasturcture
{
    public sealed class Resolver : IResolver, IDisposable
    {
        private readonly IWindsorContainer _container;

        public Resolver(string assemblyPrefix, string log4NetConfigFileName = null)
        {
            _container = Windsor.Container(assemblyPrefix, log4NetConfigFileName);
        }

        public Resolver(IEnumerable<string> assemblyPrefixes, string log4NetConfigFileName = null)
        {
            _container = Windsor.Container(assemblyPrefixes, log4NetConfigFileName);
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