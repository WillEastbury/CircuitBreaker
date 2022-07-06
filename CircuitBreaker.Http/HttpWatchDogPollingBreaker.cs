using System;
using System.Threading;
using System.Threading.Tasks;
using CircuitBreaker.Options;
using CircuitBreaker.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CircuitBreaker.Core.Enums;
namespace  CircuitBreaker.Http
{
    public class HttpWatchDogPollingBreaker : IWatchDogBreaker
    {
        public CircuitState circuitState { get; private set; } = CircuitState.Dead;
        public ICircuitResultStore circuitHistory { get; private set; }
        public ICircuitOperations circuitOps {get; private set;}
        private HttpCircuitBreakingWatchdogOptions httpPollerOptions;
        private readonly ILogger<HttpWatchDogPollingBreaker> _logger;
        private SemaphoreSlim SemPeriod = new SemaphoreSlim(1, 1);
        private bool StopRequested = false;
        public HttpWatchDogPollingBreaker(IOptions<HttpCircuitBreakingWatchdogOptions> PollerOptions,ILogger<HttpWatchDogPollingBreaker> logger, ICircuitOperations circuitOps,ICircuitResultStore circuitHistory)
        {
            this.httpPollerOptions = PollerOptions.Value;
            this._logger = logger;
            this.circuitOps = circuitOps;
            this.circuitOps.ReportStatusBackToBreakerCallback =  circuitHistory.AddNewRequestStatusAsync;
            this.circuitHistory = circuitHistory;
        }
        public async ValueTask<RequestStatusType> ExecuteRequestWithBreakerTracking(string message, string ExtraHeaderInfo)
        {
            return await circuitOps.ProcessStandardOperationalMessageAsync(message, ExtraHeaderInfo);
        }
        public async Task StartWatchDog(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !StopRequested)
            {
                await CheckCircuitForTripAsync();
                await Task.Delay(httpPollerOptions.PollingIntervalMs);
            }
        }
        public Task StopWatchDog(CancellationToken cancellationToken)
        {
            StopRequested = true;
            return Task.CompletedTask;
        }     
        private async ValueTask CheckCircuitForTripAsync()
        {
            // If the last processed test of the circuit was more than x seconds ago, then fire a test message through the breaker before calculating the status
            if (circuitHistory.LastProcessedRequest < DateTime.Now.AddSeconds(httpPollerOptions.SyntheticPulseOnIdleOrDownInSeconds * -1))
            {
                RequestStatusType rst = await circuitOps.ProcessSyntheticTestMessageAsync();
            }
            // Calculate the status of the circuit
            int ErrorPercentageThisMinute = 0, TransientErrorPercentageThisMinute = 0;
            if (circuitHistory.GetAllRequestCount() > 0)
            {
                ErrorPercentageThisMinute = (int)((((double)circuitHistory.GetStatsByRequestType(RequestStatusType.Failure) / (double)circuitHistory.GetAllRequestCount()) * 100));
                TransientErrorPercentageThisMinute = (int)((((double)circuitHistory.GetStatsByRequestType(RequestStatusType.TransientFailure) / (double)circuitHistory.GetAllRequestCount()) * 100));
            }
            if (ErrorPercentageThisMinute > httpPollerOptions.MaxErrorsPercentage) 
            {
                circuitState = CircuitState.Dead; // Main errors have crossed the threshold, so take it down.
            }
            else if (TransientErrorPercentageThisMinute > httpPollerOptions.MaxErrorsTransientPercentage) 
            {
                circuitState = CircuitState.InTrouble; // Only transient metrics are down, so throttle only.
            }
            else 
            {
                circuitState = CircuitState.OK; // All errors are inside threshold, so take it up.
            }
            _logger.LogInformation($"Circuit is {circuitState} -- Requests: {circuitHistory.GetAllRequestCount()} Stats: Err%: {ErrorPercentageThisMinute}, TrErr%: {TransientErrorPercentageThisMinute}");        } 
    }
}
