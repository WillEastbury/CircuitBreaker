using System;
using System.Threading;
using System.Threading.Tasks;
using CircuitBreaker.Core.Interfaces;
using CircuitBreaker.Core.Enums;
using CircuitBreaker.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace CircuitBreaker.Azure.ServiceBus
{
    public class ServiceBusSessionProcessorService : IHostedService, IRequestProviderHostService
    {   
        private readonly ServiceBusSessionProcessorServiceOptions serviceBusConnectionAndProcessorOptions;
        public IWatchDogBreaker poller {get; private set;} 
        private readonly ILogger<ServiceBusSessionProcessorService> _logger;
        private bool StopRequested = false;
        private SemaphoreSlim MaxSessionsInParallel = null;
        private Task pollerTask; 
        private ServiceBusClient client = null;
        public ServiceBusSessionProcessorService(IOptions<ServiceBusSessionProcessorServiceOptions> serviceBusConnectionAndProcessorOptions, ILogger<ServiceBusSessionProcessorService> logger, IWatchDogBreaker poller )
        {
            this.serviceBusConnectionAndProcessorOptions = serviceBusConnectionAndProcessorOptions.Value;
            this._logger = logger;
            this.poller = poller;
            this.MaxSessionsInParallel = new SemaphoreSlim(serviceBusConnectionAndProcessorOptions.Value.MaxSessionsInParallel, serviceBusConnectionAndProcessorOptions.Value.MaxSessionsInParallel);
        }
        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            // Can we simplify the following code? 
 
            client = new ServiceBusClient(serviceBusConnectionAndProcessorOptions.ConnectionString);
            this.pollerTask = poller.StartWatchDog(cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !StopRequested)
            {
                if (this.poller.circuitState != CircuitState.Dead)
                {
                    try
                    {
                        await using ServiceBusSessionReceiver receiver = await client.AcceptNextSessionAsync(serviceBusConnectionAndProcessorOptions.QueueName, new ServiceBusSessionReceiverOptions(){ PrefetchCount = 10, ReceiveMode = ServiceBusReceiveMode.PeekLock});

                        // Double check the circuit breaker, this could have been a quite long poll and The service might have dropped in the meantime
                        if (this.poller.circuitState != CircuitState.Dead)
                        {
                            try
                            {
                                MaxSessionsInParallel.Wait();
                                await ProcessServiceBusSession(receiver);
                            }
                            finally 
                            {
                                MaxSessionsInParallel.Release();
                                await receiver.CloseAsync();
                            }  
                        }
                        else
                        {
                            await receiver.CloseAsync();
                        }
                    }
                    catch (ServiceBusException exc) when (exc.Reason == ServiceBusFailureReason.ServiceTimeout)
                    {
                        // Nothing to process, swallow the exception and carry on with the loop.
                    }
                }
            }
            await this.poller.StopWatchDog(CancellationToken.None); 
        }
        private async Task ProcessServiceBusSession(ServiceBusSessionReceiver receiver)
        {
            try
            {
                // Triple check the state in case the target service has dropped in the meantime
                if (this.poller.circuitState != CircuitState.Dead)
                {
                    while(receiver.PeekMessageAsync() != null)
                    {   
                        var batch =  await receiver.ReceiveMessagesAsync(10, new TimeSpan(0,0,serviceBusConnectionAndProcessorOptions.SessionTimeOutInSeconds));
                        if (batch.Count > 0)
                        {
                            _logger.LogInformation($"...Batch incoming for {receiver.SessionId} - {batch.Count} Messages received.");

                            foreach(ServiceBusReceivedMessage sbrm in batch)
                            {
                                if(this.poller.circuitState == CircuitState.InTrouble)
                                {
                                    await Task.Delay(serviceBusConnectionAndProcessorOptions.OverloadedDelayInMs);
                                }
                                if(await ProcessMessageRecursiveRetryAsync(sbrm) == RequestStatusType.Success)
                                {
                                    await receiver.CompleteMessageAsync(sbrm); 
                                }
                                else
                                {
                                    await receiver.AbandonMessageAsync(sbrm);
                                }

                                await receiver.RenewSessionLockAsync();

                            }

                        }
                        else
                        {
                            break; // No more messages in the session, so we can exit the loop.
                        }
                    }
                }
                else
                {
                    await receiver.CloseAsync();
                }
            }
            catch (Exception exc)
            {
                _logger.LogError($"Error processing session {receiver.SessionId} - {exc.Message}");
                await receiver.CloseAsync();
            }
        }
        private async Task<RequestStatusType> ProcessMessageRecursiveRetryAsync(ServiceBusReceivedMessage sbrm, int currentRetries = 0)
        {
            if (currentRetries < serviceBusConnectionAndProcessorOptions.MaxRetryCount)
            {
                var req = await poller.ExecuteRequestWithBreakerTracking(sbrm.ToString(), $"Retry {currentRetries}");
                if(req != RequestStatusType.Success)
                {
                    // Recursive Retry, incrementing till we hit MaxRetryCount, whilst waiting for the delay (that extends as a multiple of the number of retries)
                    await Task.Delay(serviceBusConnectionAndProcessorOptions.RetryDelay * currentRetries);
                    return await ProcessMessageRecursiveRetryAsync(sbrm, currentRetries + 1);
                }
                else
                {
                    return req; // success! We can return the status.
                }
            }
            else
            {
                _logger.LogError($"Max Retries of {serviceBusConnectionAndProcessorOptions.MaxRetryCount} Exceeded {currentRetries} for Message {sbrm.MessageId}");
                return RequestStatusType.Failure;
            }
        }
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            StopRequested = true; 
            return Task.CompletedTask;
        }
    }
}
