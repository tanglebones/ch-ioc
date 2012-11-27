using System.Diagnostics;
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
        public void TestArray()
        {
            var testHost = _resolver.Resolve<ITestHostArr>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
            var registered = _resolver.Registered();
            foreach (var reg in registered)
            {
                Debug.WriteLine(reg.Item1 + " " + string.Join(",", reg.Item2));
            }
        }

        [Test]
        public void TestEnum()
        {
            var testHost = _resolver.Resolve<ITestHostEnum>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
        }
    }
}