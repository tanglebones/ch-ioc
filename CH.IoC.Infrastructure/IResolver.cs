using System;
using System.Collections.Generic;

namespace CH.IoC.Infrastructure
{
    public interface IResolver
    {
        T Resolve<T>() where T:class;
        T[] ResolveAll<T>() where T : class;
        IEnumerable<Tuple<string, IEnumerable<string>>> Registered();

        void LoadDynamicAssemblies(
            IEnumerable<string> includePrefixes,
            IEnumerable<string> excludePrefixes,
            IEnumerable<string> directories);
    }
}