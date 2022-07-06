using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using CircuitBreaker.Core.Interfaces;
using CircuitBreaker.Core.Enums;
using CircuitBreaker.Options;
namespace CircuitBreaker.Core.Base
{
    public class RingCircuitResultStore : ICircuitResultStore
    {
        private RingCircuitResultStoreOptions options;
        private int NextWritingPositionInArray = 0;
        private int MaxHistoryLength = 100;
        public DateTime LastProcessedRequest { get; private set; } = DateTime.Now.Subtract(new TimeSpan(1,0,0));
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private RequestStatusType[] requests;
        public RingCircuitResultStore(IOptions<RingCircuitResultStoreOptions> options)
        {
            this.options = options.Value;
            this.MaxHistoryLength = this.options.MaxHistoryInRingBuffer;
            this.requests = new RequestStatusType[MaxHistoryLength];
        }

        // This is intended to be passed around to other classes as a callback method / delegate to update the circuit history Ring Buffer
        // The ICircuitOperations implementer will then call this method when it receives a new request status.
        // You'll need to assign this to the ICircuitOperations.UpdateCircuitBreakerRequestStatusAsync property.
        // (public Func<RequestStatusType, ValueTask> UpdateCircuitBreakerRequestStatusAsync { get; set; })
        public virtual async ValueTask AddNewRequestStatusAsync(RequestStatusType requestStatus)
        {
            await Task.Delay(1);
            _semaphore.Wait();
            LastProcessedRequest = DateTime.Now;
            requests[NextWritingPositionInArray] = requestStatus;
            // Update the next write position for the ring buffer, cycling to zero if we move to MaxHistoryLength
            NextWritingPositionInArray++;
            if (NextWritingPositionInArray >= (MaxHistoryLength))
            {
                // Roll around to the beginning of the array
                NextWritingPositionInArray = 0;
            }
            _semaphore.Release();

        }
        public virtual int GetStatsByRequestType(RequestStatusType rst) => requests.Where(x => x == rst).Count();
        public virtual int GetAllRequestCount() => requests.Where(x => x != RequestStatusType.NotSet).Count();
    }
}