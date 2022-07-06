using System;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CircuitBreaker.Core.Interfaces;
using CircuitBreaker.Options;
namespace CircuitBreaker.Hosting.Extensions
{
    // This is a helper class to configure the hosted service and its dependencies.
    // It is intended to be used in the ConfigureServices method of the IHostBuilder class.
    // The IHostBuilder class is used to create the IHost instance.
    
    public static class ProcessorSetupExtensions
    {
        public static IServiceCollection ConfigureIRequestProviderHostService<T,U,V,W>(this IServiceCollection services, HostBuilderContext hostContext) 
            where T : class, ICircuitOperations 
            where U : class, IWatchDogBreaker
            where V : class, ICircuitResultStore
            where W : class, IRequestProviderHostService, IHostedService
{
            services.Configure<ServiceBusSessionProcessorServiceOptions>(hostContext.Configuration.GetSection("ServiceBusProcessor"));
            services.Configure<HttpCircuitBreakingWatchdogOptions>(hostContext.Configuration.GetSection("HttpWatchdog"));
            services.Configure<RingCircuitResultStoreOptions>(hostContext.Configuration.GetSection("RingBuffer"));
            
            HttpCircuitBreakingWatchdogOptions optionsSet = services.BuildServiceProvider().GetService<IOptions<HttpCircuitBreakingWatchdogOptions>>().Value;
            services.AddHttpClient("WatchDogClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.PollingUrlBase);});
            services.AddHttpClient("OperationalClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.OperationsUrlBase);});
            services.AddHttpClient("PlainOldClient").ConfigureHttpClient(client => {client.BaseAddress = new Uri(optionsSet.PollingUrlBase);});
            services.AddICircuitOperations<T>();
            services.AddIWatchDogBreaker<U>();
            services.AddICircuitResultStore<V>();
            services.AddIRequestProviderHostService<W>();
            return services;
        } 
        public static void AddICircuitOperations<T>(this IServiceCollection services) where T : class, ICircuitOperations
        {
            services.AddSingleton<ICircuitOperations,T>();
        }
        public static void AddICircuitResultStore<T>(this IServiceCollection services) where T :  class, ICircuitResultStore
        {
            services.AddSingleton<ICircuitResultStore,T>();
        }
        public static void AddIWatchDogBreaker<T>(this IServiceCollection services) where T :  class, IWatchDogBreaker
        {
            services.AddSingleton<IWatchDogBreaker,T>();
        }
        public static void AddIRequestProviderHostService<T>(this IServiceCollection services) where T : class, IHostedService, IRequestProviderHostService
        {
            services.AddHostedService<T>();
            
        }
    }
}
