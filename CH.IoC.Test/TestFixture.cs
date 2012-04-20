using System.Linq;
using CH.IoC.Intrasturcture;
using CH.IoC.TestHost.Interface;
using NUnit.Framework;

namespace CH.IoC.Test
{
    [TestFixture]
    public class TestFixture
    {
        [Test]
        public void Test()
        {
            using (var resolver = new Resolver("CH.IoC"))
            {
                var testHost = resolver.Resolve<ITestHost>();
                var results = testHost.Run("test").ToArray();
                Assert.That(results.Any(x => x == "ONE: test"));
                Assert.That(results.Any(x => x == "TWO: test"));
            }
        }
    }
}