using Microsoft.Extensions.Hosting;
using CircuitBreaker.Core.Base;
using CircuitBreaker.Hosting.Extensions;
using CircuitBreaker.Azure.ServiceBus;
using CircuitBreaker.Http;
using CircuitBreaker.Custom.CircuitOperations;

namespace CircuitBreaker
{
    class Program
    {   
        public static void Main(string[] args)
        {
            IHost ApplicationHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) => 
            {    
                // Add the hosted service and run the host, choose one of these two ICircuitOperations implementations to get started then build your own and plug them in ! 

                services.ConfigureIRequestProviderHostService<DemoCustomerCircuitOperations, HttpWatchDogPollingBreaker, RingCircuitResultStore, ServiceBusSessionProcessorService>(hostContext);
                //services.ConfigureIRequestProviderHostService<HttpCircuitOperations, HttpWatchDogPollingBreaker, RingCircuitResultStore, ServiceBusSessionProcessorService>(hostContext);
            
            }).Build();
            ApplicationHost.Run();
        }
    }
}