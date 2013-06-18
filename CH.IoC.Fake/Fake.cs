using System;
using System.Linq;
using System.Reflection;
using FakeItEasy;

namespace CH.IoC.Fake
{
    public static class Fake
    {

        public static T Create<T>(params object[] mockHandlers)
        {
            var type = typeof (T);
            return CreateType<T>(type, mockHandlers);
        }

        public static T CreateType<T>(Type type, object[] mockHandlers)
        {
            ConstructorInfo constructor;
            var parameters = DependenciesAsConstructorParameters(type, out constructor, mockHandlers);
            return (T) (parameters.Length == 0 ? constructor.Invoke(null) : constructor.Invoke(parameters));
        }

        private static object[] DependenciesAsConstructorParameters(Type type, out ConstructorInfo constructor,
                                                                    params object[] mockHandlers)
        {
            // we include both public and internal constructors
            var constructors =
                type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            constructor = null;
            // if there is more than one constructor and mock parameters are passed
            if (constructors.Length > 1 && mockHandlers.Length > 0)
            {
                // skip empty constructors
                foreach (var c in constructors.Where(c => c.GetParameters().Length > 0))
                {
                    constructor = c;
                    break;
                }
            }
            if (constructor == null && constructors.Length > 0) constructor = constructors[0];
            ThrowIfConstructorIsNull(type, constructor);
            var fakeMethod = typeof (A).GetMethod("Fake", new Type[] {});
// previously checked in method ThrowIfConstructorIsNullNoCover
// ReSharper disable PossibleNullReferenceException
            var parameters = constructor.GetParameters();
// ReSharper restore PossibleNullReferenceException
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var actionOfType = typeof (Action<>).MakeGenericType(new[] {parameterType});
                var funcOfType = typeof (Func<>).MakeGenericType(new[] {parameterType});
                var actionAndObjectOfType = typeof (ActionAndObjectOf<>).MakeGenericType(new[] {parameterType});
                object fake = null;
                if (mockHandlers != null)
                {
                    foreach (var each in mockHandlers)
                    {
                        var candidateType = each.GetType();
                        if (candidateType == actionAndObjectOfType)
                        {
                            var actionAndObject = (ActionAndObject) each;
                            actionAndObject.Action.DynamicInvoke(actionAndObject.Object);
                            fake = actionAndObject.Object;
                        }
                        else if (candidateType == funcOfType)
                        {
                            fake = ((Delegate) each).DynamicInvoke();
                        }
                        else if (candidateType == actionOfType)
                        {
                            ((Delegate) each).DynamicInvoke(fake);
                        }
                        else if (candidateType.FindInterfaces((t, o) => ( t.FullName == o.ToString()) , parameterType.FullName).Any())
                        {
                            fake = each;
                        }
                    }
                }
                if (fake == null)
                {
                    var fakeGenericMethod = fakeMethod.MakeGenericMethod(parameterType);
                    fake = fakeGenericMethod.Invoke(null, new object[] { });
                }
                args[i] = fake;
            }
            return args;
        }


// unused by design
// ReSharper disable UnusedParameter.Local
        private static void ThrowIfConstructorIsNull(Type type, ConstructorInfo constructor)
// ReSharper restore UnusedParameter.Local
        {
            if (constructor == null)
            {
                throw new InvalidOperationException("Dependency object " + type + " does not have constructor");
            }
        }

        public static Action<T> Mock<T>(Action<T> act) where T : class
        {
            return act;
        }

        public static ActionAndObjectOf<T> Mock<T>(out T toSet) where T : class
        {
            return Mock(out toSet, o => { });
        }

        public static ActionAndObjectOf<T> Mock<T>(out T toSet, Action<T> act) where T : class
        {
            toSet = A.Fake<T>();
            return new ActionAndObjectOf<T> {Action = act, Object = toSet};
        }

        public static Func<T> Set<T>(Func<T> set) where T : class
        {
            return set;
        }
    }

// required for instantiation
// ReSharper disable UnusedTypeParameter
    public class ActionAndObjectOf<T> : ActionAndObject
    {
    }

// ReSharper restore UnusedTypeParameter
    public abstract class ActionAndObject
    {
        public Delegate Action { get; set; }
        public object Object { get; set; }
    }
}