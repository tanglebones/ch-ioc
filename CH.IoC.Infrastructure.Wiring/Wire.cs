using System;

namespace CH.IoC.Infrastructure.Wiring
{
    public sealed class Wire : Attribute
    {
        public Type InterfaceType { get; private set; }

        public Wire()
        {
            InterfaceType = null;
        }

        public Wire(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }
    }
}