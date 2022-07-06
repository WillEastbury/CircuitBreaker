using System.Threading;
using System.Threading.Tasks;
namespace CircuitBreaker.Core.Interfaces
{
    public interface IRequestProviderHostService
    {
        IWatchDogBreaker poller {get;} 
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
