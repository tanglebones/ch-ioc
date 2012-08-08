using System;
using System.Collections.Generic;
using System.Linq;
using Castle.Core;
using Castle.Windsor;

namespace CH.IoC.Infrastructure
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

        public IEnumerable<T> ResolveAll<T>()
        {
            return _container.ResolveAll<T>();
        }

        public IEnumerable<Tuple<string, IEnumerable<string>>> Registered()
        {
            return from ComponentModel obj in _container.Kernel.GraphNodes select Tuple.Create(obj.Name, obj.Services.Select(s => s.FullName));
        }

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}