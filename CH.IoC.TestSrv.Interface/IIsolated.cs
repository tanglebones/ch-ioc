using System.Threading;
using System.Threading.Tasks;

namespace CH.IoC.TestSrv.Interface
{
    public interface IIsolated
    {
        Task Run(CancellationToken token);
    }
}