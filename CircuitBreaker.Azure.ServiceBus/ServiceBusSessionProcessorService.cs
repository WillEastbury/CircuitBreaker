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
    public class ServiceBusSessionProcessorService : BackgroundService, IHostedService, IRequestProviderHostService
    {   
        private readonly ServiceBusSessionProcessorServiceOptions serviceBusConnectionAndProcessorOptions;
        public IWatchDogBreaker poller {get; private set;} 
        private readonly ILogger<ServiceBusSessionProcessorService> _logger;
        private SemaphoreSlim MaxSessionsInParallel = null;
        private Task pollerTask; 
        private ServiceBusClient client = null;
        public ServiceBusSessionProcessorService(IOptions<ServiceBusSessionProcessorServiceOptions> serviceBusConnectionAndProcessorOptions, ILogger<ServiceBusSessionProcessorService> logger, IWatchDogBreaker poller )
        {

            // Create the client
            // Open The Queue
            // For each entity
            // Create a session with the session id set to the entity
            // Send a stream of messages down *each* session 
            // close the session
            // loop

            // Create the client
            // Open the queue
            // for each session (parallel multithreaded processing)
                // Wait for a lock to be available (up to 100 available), block the thread if it is not 
                // consume a session with the session id set to the session id
                // process all of the messages for that session id (this entity)
                // if there are no more messages, close the session and move on to the next session
                // release the lock and move to the next session
            // 
            
            this.serviceBusConnectionAndProcessorOptions = serviceBusConnectionAndProcessorOptions.Value;
            this._logger = logger;
            this.poller = poller;

            this.MaxSessionsInParallel = new SemaphoreSlim(serviceBusConnectionAndProcessorOptions.Value.MaxSessionsInParallel, serviceBusConnectionAndProcessorOptions.Value.MaxSessionsInParallel);
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
                var req = await poller.ExecuteRequestWithBreakerTracking(sbrm.Body.ToString(), $"Retry {currentRetries}");
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
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            client = new ServiceBusClient(serviceBusConnectionAndProcessorOptions.ConnectionString);
            this.pollerTask = poller.StartWatchDog(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (this.poller.circuitState != CircuitState.Dead)
                    {
                        try
                        {
                            await using ServiceBusSessionReceiver receiver = await client.AcceptNextSessionAsync(serviceBusConnectionAndProcessorOptions.QueueName, new ServiceBusSessionReceiverOptions() { PrefetchCount = 10, ReceiveMode = ServiceBusReceiveMode.PeekLock });
                            // Double check the circuit breaker, this could have been a quite long poll and The service might have dropped in the meantime
                            if (this.poller.circuitState != CircuitState.Dead)
                            {
                                try
                                {
                                    // Critical section here - only ever process MaxSessionsInParallel sessions at a time
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
                catch (TaskCanceledException)
                {
                    // This is a normal exception, just exit the loop.
                    _logger.LogError("Shutdown Requested");
                    break;
                }
            }
        }
    }
}
