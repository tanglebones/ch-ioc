using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CH.IoC.Infrastructure;
using CH.IoC.TestHost.Interface;
using CH.IoC.TestLog;
using CH.IoC.TestPlugin.Interface;
using CH.IoC.TestSrv.Interface;
using NUnit.Framework;

namespace CH.IoC.Test
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable"), TestFixture]
    public sealed class TestFixture
    {
        [TestFixtureSetUp]
        public void Setup()
        {
            Environment.CurrentDirectory =
#if DEBUG
                "../../../deploy/Debug";
#else
                "../../../deploy/Release";
#endif
        }

        [Test]
        public void TestArray()
        {
            IResolver resolver = new Resolver(new []{"CH.IoC."});
            resolver.LoadDynamicAssemblies(new[]{Environment.CurrentDirectory});
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
                var re = registered.FirstOrDefault(x => x.Item1.StartsWith(entry.i));
                Assert.NotNull(re);
                foreach (var ce in entry.c)
                {
                    Assert.Greater(re.Item2.Count(x=>x.StartsWith(ce)),0);
                }
            }
        }

        [Test]
        public void TestEnum()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            resolver.LoadDynamicAssemblies(new[] { Environment.CurrentDirectory });
            var testHost = resolver.Resolve<ITestHostEnum>();
            var results = testHost.Run("test").ToArray();
            Assert.That(results.Any(x => x == "ONE: test"));
            Assert.That(results.Any(x => x == "TWO: test"));
        }

        [Test]
        public void TestMissing()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            resolver.LoadDynamicAssemblies(new[] { Environment.CurrentDirectory });
            Assert.Throws<Exception>(() => resolver.Resolve<IQueryable>());
        }

        [Test]
        public void TestResolveAll()
        {
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            resolver.LoadDynamicAssemblies(new[] { Environment.CurrentDirectory });
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
            resolver.LoadDynamicAssemblies( new[] { Environment.CurrentDirectory });
            {
                var q = resolver.ResolveAll<IDoSomething>().ToArray();
                var p = resolver.ResolveAll<IDoSomething>().ToArray();

                for (var i = 0; i < q.Length; i++)
                {
                    Assert.AreEqual(q[i],p[i]);
                }
            }
        }

        [Test]
        public void TestIsolatedDomainResolves()
        {
            // Test (Srv) ref's ILog 4.0.0.0
            // Boot       ref's ILog 4.1.0.0
            // Isolated/0 ref's ILog 4.0.0.0
            // Isolated/1 ref's ILog 4.1.0.0
            // Isolated/2 ref's ILog 4.2.0.0
            IResolver resolver = new Resolver(new[] { "CH.IoC." });
            resolver.LoadDynamicAssemblies(new[] { "Boot", Environment.CurrentDirectory });
            var boot = resolver.Resolve<IBoot>();
            var log = resolver.Resolve<ILog>();
            log.Log("Test");
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    cts.CancelAfter(5000);
                    Debug.WriteLine("Start Boot");
                    boot.Run(cts.Token).Wait();
                }
                catch (AggregateException ae)
                {
                    foreach (var e in ae.InnerExceptions.Where(e => !(e is TaskCanceledException)))
                    {
                        log.Log("Test aEx: " + e);
                    }
                }
                catch (Exception ex)
                {
                    log.Log("Test Ex: " + ex);
                }
                Debug.WriteLine("Finish Boot");
            }
            log.Log("Test Done");
        }
    }
}