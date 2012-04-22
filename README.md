#ch-ioc

IoC for Infrastructure wiring.

One use of IoC containers for wiring up the infrastructure of a program at startup. These projects aim to make that setting up infrasture easier.

Infrasture classes used to be implemented as static classed, and would directly reference each other. This makes testing infrastructure difficult as there is no way to decouple them and mock their dependents.

Using IoC we can build a container of singletons for our infrasture and have each infrastructure class implement an interface and take into its constrcutor the interfaces of the other infrastructure classes it uses. Now to test an infrastructure class we can create an instance of it using mocks for the other infrastructure systems it uses.

We also want to be able to address multiple sub-systems that provide a common interface. Plugins (think photoshop) fall into this category, as they are loaded once at start up. If a constructure takes in an IEnumerable<interface_type> it will get an enumerable of all the infrastucture components that implement the interface_type. The bootstrapping code that builds the container will scan the executable directory for all dll's matching a prefix and load them, so plugins can be dropped into place.

Example Usage: See the CH.Ioc.Test projects for an example of how this should be used.

Note: The TestHost and TestPlugin output paths are set to build to the Test project's bin directory so the components will be loaded dynamically.

## CH.IoC.Infrastructure

Provides a Resolver class. This should be constructed once during your programs bootstrapping process, and disposed of once during your program shutdown. It uses Windor as the backend, so you can use the Windor PerWebRequest lifetime when using IIS. Programs should have a main interface (host, etc.) that they resolve and pass off execution too. The components in the infracture should have no knowledge of the resolver since the resolver doesn't provide support for life times beyond a single execution thread. (For lifetimes life pooled or transient you should create a infrastructure component that is a factory for those objects and not use Windows lifetime system, since that would mix the concerns the container is addressing and be a single responsibility principle violation.)

Only the program should reference this assembly.

## CH.IoC.Infrastructure.Wiring

All infrastructure components should reference this assembly, and not the Infrastructure assembly. Classes that want to be wired by default (non-test environments) as their interfaces (yes, all of them) should be marked with the Wire attribute. You can provide a like a types to the attribute if you don't want all the interfaced wired.

## CH.IoC.Test...

These assemblies are a test/example of how to use the system.

Note: Since there are no direct dependencies between .Test and .TestHost and .TestPlugin you must build .TestHost and .TestPlugin explicitly before running .Test. This is by design, since we want to decouple the execution environment for the concrete components it executes.