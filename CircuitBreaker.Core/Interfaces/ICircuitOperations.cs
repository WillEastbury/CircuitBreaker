using System.Net.Http;
using System.Threading.Tasks;
using System;
using CircuitBreaker.Core.Enums;

namespace CircuitBreaker.Core.Interfaces
{
    public interface ICircuitOperations
    {
        Func<RequestStatusType, ValueTask> ReportStatusBackToBreakerCallback { get; set; }
        ValueTask<HttpRequestMessage> GetPreparedRequestAsync(string tailUri, HttpMethod method,string ContentBody, string BearerToken, Func<string, Task<string>> getBearerTokenCallbackAsync, string ContentType, string Accept, string ExtraHeaderInfo);
        Task<RequestStatusType> ProcessStandardOperationalMessageAsync(string message, string ExtraHeaderInfo);
        Task<RequestStatusType> ProcessSyntheticTestMessageAsync();
        ValueTask<RequestStatusType> FireRequestBehindBreakerAsync(string NameOfClient, string tailUri, HttpMethod method,string ContentBody, string BearerToken, Func<string, Task<string>> getBearerTokenCallbackAsync, string ContentType, string Accept, string ExtraHeaderInfo);

    }
}