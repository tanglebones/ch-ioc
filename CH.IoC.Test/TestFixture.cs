using System;
using System.Diagnostics;
using System.Linq;
using CH.IoC.Infrastructure;
using CH.IoC.TestHost.Interface;
using CH.IoC.TestPlugin.Interface;
using NUnit.Framework;

namespace CH.IoC.Test
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), TestFixture]
    public sealed class TestFixture
    {
        [Test]
        public void TestArray()
        {
            IResolver resolver = new Resolver("CH.IoC.");
            var testHost = resolver.Resolve<ITestHostArr>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
            var registered = resolver.Registered().ToArray();
            foreach (var reg in registered)
            {
                Debug.WriteLine(reg.Item1 + " " + string.Join(",", reg.Item2));
            }
            foreach (var entry in new[]
                {
                    new
                        {
                            i = "CH.IoC.TestPlugin.Interface.IDoSomething",
                            c = new[] {"CH.IoC.TestPlugin.DoSomethingOne", "CH.IoC.TestPlugin.DoSomethingTwo"}
                        },
                    new {i = "CH.IoC.TestPlugin.IOnePrefix", c = new[] {"CH.IoC.TestPlugin.OnePrefix"}},
                    new {i = "CH.IoC.TestHost.Interface.ITestHostEnum", c = new[] {"CH.IoC.TestHost.TestHostEnumWirer"}},
                    new
                        {
                            i = "CH.IoC.TestHost.Interface.ITestHost",
                            c = new[] {"CH.IoC.TestHost.TestHostArr"}
                        },
                    new {i = "CH.IoC.TestHost.Interface.ITestHostArr", c = new[] {"CH.IoC.TestHost.TestHostArr"}}
                })
            {
                var re = registered.FirstOrDefault(x => x.Item1 == entry.i);
                Assert.NotNull(re);
                foreach (var ce in entry.c)
                {
                    Assert.That(re.Item2.Contains(ce));
                }
            }
        }

        [Test]
        public void TestEnum()
        {
            IResolver resolver = new Resolver(new[]{"CH.IoC."});
            var testHost = resolver.Resolve<ITestHostEnum>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
        }

        [Test]
        public void TestMissing()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            Assert.Throws<Exception>(()=>resolver.Resolve<IQueryable>());
        }

        [Test]
        public void TestResolveAll()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            {
                var q = resolver.ResolveAll<IDoSomething>().ToArray();
                Assert.Greater(q.Length, 1);
                foreach (var e in q)
                {
                    var type = e.GetType();
                    Assert.IsFalse(type.IsInterface);
                    Assert.NotNull(type.GetInterface(typeof (IDoSomething).FullName));
                }
            }
            {
                var q = resolver.ResolveAll<IQueryable>().ToArray();
                Assert.AreEqual(0,q.Length);
            }
        }

        [Test]
        public void TestDoubleResolve()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            {
                var q = resolver.ResolveAll<IDoSomething>().ToArray();
                var p = resolver.ResolveAll<IDoSomething>().ToArray();

                for (var i = 0; i < q.Length; i++)
                {
                    Assert.AreEqual(q[i],p[i]);
                }
            }
        }
    }
}