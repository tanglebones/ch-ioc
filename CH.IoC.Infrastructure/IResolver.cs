namespace CH.IoC.Infrastructure
{
    public interface IResolver
    {
        T Resolve<T>();
    }
}