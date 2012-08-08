using System;
using System.Collections.Generic;

namespace CH.IoC.Infrastructure
{
    public interface IResolver
    {
        T Resolve<T>();
        IEnumerable<T> ResolveAll<T>();
        IEnumerable<Tuple<string, IEnumerable<string>>> Registered();
    }
}