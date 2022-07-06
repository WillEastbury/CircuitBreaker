using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using CircuitBreaker.Core.Base;
using CircuitBreaker.Extensions;
using CircuitBreaker.Options;
using CircuitBreaker.Azure.ServiceBus;
using CircuitBreaker.Http;
namespace CircuitBreaker
{
    class Program
    {   
        public static void Main(string[] args)
        {
            IHost ApplicationHost = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) => 
                {
                    services.Configure<ServiceBusSessionProcessorServiceOptions>(hostContext.Configuration.GetSection("ServiceBusProcessor"));
                    services.Configure<HttpCircuitBreakingWatchdogOptions>(hostContext.Configuration.GetSection("HttpWatchdog"));
                    services.Configure<RingCircuitResultStoreOptions>(hostContext.Configuration.GetSection("HttpWatchdog"));
                    
                    HttpCircuitBreakingWatchdogOptions optionsSet = services.BuildServiceProvider().GetService<IOptions<HttpCircuitBreakingWatchdogOptions>>().Value;
                    services.AddHttpClient("WatchDogClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.PollingUrlBase);});
                    services.AddHttpClient("OperationalClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.OperationsUrlBase);});
                    services.AddHttpClient("PlainOldClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.PollingUrlBase);});

                    // Add the hosted service
                    services.ConfigureIRequestProviderHostService<HttpCircuitOperations, HttpWatchDogPollingBreaker, RingCircuitResultStore, ServiceBusSessionProcessorService>();
                }
            ).Build();
            ApplicationHost.Run();
        }
    }
}