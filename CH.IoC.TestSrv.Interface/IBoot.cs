using System.Threading;
using System.Threading.Tasks;

namespace CH.IoC.TestSrv.Interface
{
    public interface IBoot
    {
        Task Run(CancellationToken token);
    }
}
