namespace CH.IoC.Infrasturcture
{
    public interface IResolver
    {
        T Resolve<T>();
    }
}