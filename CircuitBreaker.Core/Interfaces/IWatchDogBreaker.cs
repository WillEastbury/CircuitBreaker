using System.Threading;
using System.Threading.Tasks;
using CircuitBreaker.Core.Enums;

namespace CircuitBreaker.Core.Interfaces
{
    // This interface is used to provide the ability to report the status of a request to the breaker.
    // and start and stop the watchdog
    // It also holds an Instance of the current circuit state and the history of the circuit's last few requests 
    public interface IWatchDogBreaker
    {
        CircuitState circuitState { get; }
        ICircuitResultStore circuitHistory { get; }
        ICircuitOperations circuitOps { get; }
        ValueTask<RequestStatusType> ExecuteRequestWithBreakerTracking(string message, string ExtraHeaderInfo);
        Task StartWatchDog(CancellationToken cancellationToken);

    }
}
