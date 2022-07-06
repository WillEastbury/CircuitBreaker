using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CircuitBreaker.Core.Interfaces;

namespace CircuitBreaker.Extensions
{
    public static class ProcessorSetupExtensions
    {
        public static IServiceCollection ConfigureIRequestProviderHostService<T,U,V,W>(this IServiceCollection services) 
            where T : class, ICircuitOperations 
            where U : class, IWatchDogBreaker
            where V : class,  ICircuitResultStore
            where W : class, IRequestProviderHostService, IHostedService
        {
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
