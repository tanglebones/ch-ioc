using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Policy;

namespace CH.IoC.TestBoot
{
    public sealed class Isolate<TC, TI> : IDisposable
        where TC : MarshalByRefObject
        where TI : class
    {
        private readonly TI _value;
        private AppDomain _domain;
        static Isolate()
        {
            AssemblyHelper.Setup();
        } 

        public Isolate(Evidence evidence, AppDomainSetup appDomainSetup)
        {
            var type = typeof (TC);
            _domain = AppDomain.CreateDomain(
                type.AssemblyQualifiedName + ".isolated." + Guid.NewGuid(),
                evidence ?? AppDomain.CurrentDomain.Evidence,
                appDomainSetup ?? AppDomain.CurrentDomain.SetupInformation
                );

            _domain.DoCallBack(AssemblyHelper.Setup);

            try
            {
                var instance = _domain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);
                _value = (TI) instance;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public TI Value
        {
            get { return _value; }
        }

        [ExcludeFromCodeCoverage]
        public void Dispose()
        {
            if (_domain == null)
                return;
            AppDomain.Unload(_domain);
            _domain = null;
        }
    }
}

 