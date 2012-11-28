using System;
using System.Collections.Generic;

namespace CH.IoC.Infrastructure
{
    public interface IResolver
    {
        T Resolve<T>() where T:class;
        T[] ResolveAll<T>() where T : class;
        void Register<T>(T instance);
        IEnumerable<Tuple<string, IEnumerable<string>>> Registered();
    }
}