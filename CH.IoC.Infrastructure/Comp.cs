using System;
using System.Collections.Generic;

namespace CH.IoC.Infrastructure
{
    internal class Comp<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _func;

        public Comp(Func<T, T, int> func)
        {
            _func = func;
        }

        public int Compare(T x, T y)
        {
            return _func(x, y);
        }
    }
}