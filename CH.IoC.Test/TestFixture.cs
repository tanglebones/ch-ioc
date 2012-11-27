using System.Linq;
using CH.IoC.Infrastructure;
using CH.IoC.TestHost.Interface;
using NUnit.Framework;

namespace CH.IoC.Test
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), TestFixture]
    public sealed class TestFixture
    {
        private IResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new Resolver("CH.IoC.");
        }

        [Test]
        public void Test()
        {
            var testHost = _resolver.Resolve<ITestHost>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
        }
    }
}