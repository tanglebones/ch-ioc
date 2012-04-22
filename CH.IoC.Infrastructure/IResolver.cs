namespace CH.IoC.Intrasturcture
{
    public interface IResolver
    {
        T Resolve<T>();
    }
}