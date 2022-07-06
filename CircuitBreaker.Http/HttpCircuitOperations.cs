using System;
using System.Net.Http;
using System.Threading.Tasks;
using CircuitBreaker.Core.Enums;
using CircuitBreaker.Core.Interfaces;
using Microsoft.Extensions.Logging;
namespace CircuitBreaker.Http
{
    public class HttpCircuitOperations : ICircuitOperations
    {
        protected ILogger<HttpCircuitOperations> _logger;
        protected IHttpClientFactory _clientfactory;
        public Func<RequestStatusType, ValueTask> ReportStatusBackToBreakerCallback { get; set; } // This is the callback to report status back up to the breaker / watchdog.
        public HttpCircuitOperations(ILogger<HttpCircuitOperations> logger, IHttpClientFactory clientfactory)
        {
            _logger = logger;
            _clientfactory = clientfactory;
        }

        // This method is called before each request to prepare the HttpRequestMessage to be sent down the pipeline
        // We should set content-type and auth headers etc here HttpMethod will default to HttpMethod.Get if not specified or is null
        // Pass in an async method returning a string for the last parameter getBearerTokenCallbackAsync() if you want us to invoke that method to acquire a bearer token for you.
        // We will pass in the tailUri so your code can determine the correct endpoint to acquire the correct token.
        public virtual async ValueTask<HttpRequestMessage> GetPreparedRequestAsync(string tailUri = "/", HttpMethod method = null, string UTF8ContentBody = "", string BearerToken = "", Func<string, Task<string>> getBearerTokenCallbackAsync = null, string ContentType = "application/json", string Accept = "application/json", string ExtraHeaderInfo = "")
        {
            HttpRequestMessage RequestMessage = new HttpRequestMessage(method ?? HttpMethod.Get, tailUri);
            if (BearerToken != "")
            {
                RequestMessage.Headers.Add("Authorization", $"Bearer {BearerToken}");
            }
            else if (getBearerTokenCallbackAsync != null)
            {
                RequestMessage.Headers.Add("Authorization", $"Bearer {await getBearerTokenCallbackAsync(tailUri)}");
            }
            RequestMessage.Headers.Add("Accept", Accept);
            RequestMessage.Headers.Add("X-Extra-HeaderInfo", ExtraHeaderInfo);

            if (UTF8ContentBody != "" && (method ?? HttpMethod.Get) != HttpMethod.Get) 
            { 
                RequestMessage.Content = new StringContent(UTF8ContentBody, System.Text.Encoding.UTF8, ContentType);
            }
            return RequestMessage;
        }

        // This method is called when the circuit is opened to pass a request down the pipeline for live transactions
        // The Validate Success Method will be called to determine if the circuit breaker should trip or not.
        public virtual async Task<RequestStatusType> ProcessStandardOperationalMessageAsync(string message, string ExtraHeaderInfo)
        {
            // Decode the payload from the ingress service here and do something with it
            return await FireRequestBehindBreakerAsync("OperationalClient", "/transaction/1234", HttpMethod.Post, message, ExtraHeaderInfo: ExtraHeaderInfo);
        }

        // This method is called when the circuit is open to pass synthetic transactions in much the same vein as above.
        public virtual async Task<RequestStatusType> ProcessSyntheticTestMessageAsync()
        {
            return await FireRequestBehindBreakerAsync("WatchDogClient", "/test", HttpMethod.Get, "This is a synthetic message", ExtraHeaderInfo: "Test");
        }

        // This is a helper method to work with the HttpClient object processing the request through the circuit breaker
        // It will return a RequestStatusType object with the status of the request.If the NameOfHttpClient is specified as the default  of PlainOldClient then you wll need to specify 
        // a FULL URL (including https://f.q.d.n to the endpoint rather than just the tail in the tailUri parameter.
        // If you specify another named client then you will need to have registered that with the DI container at startup.
        // This will fire the callback pointed to UpdateCircuitBreakerRequestStatusAsync to update the circuit breaker status.

        public virtual async ValueTask<RequestStatusType> FireRequestBehindBreakerAsync(string NameOfClient = "PlainOldClient", string tailUri = "/", HttpMethod method = null, string ContentBodyUTF8 = "", string BearerToken = "", Func<string, Task<string>> getBearerTokenCallbackAsync = null, string ContentType = "application/json", string Accept = "application/json", string ExtraHeaderInfo = "")
        {
            RequestStatusType _rst = RequestStatusType.Failure;
            try
            {
                using (HttpClient _client = _clientfactory.CreateClient(NameOfClient))
                {
                    HttpRequestMessage _req =  await GetPreparedRequestAsync(tailUri, method, ContentBodyUTF8, BearerToken, getBearerTokenCallbackAsync, ContentType, Accept, ExtraHeaderInfo);
                    HttpResponseMessage _resp = await _client.SendAsync(_req);
                    _rst = await ValidateSuccess(_resp);
                }
                await ReportStatusBackToBreakerCallback(_rst);
                return _rst;
            }
            catch (Exception Ex)
            {
                _logger.LogError(Ex, "Exception in FireRequestBehindBreakerAsync");
                await ReportStatusBackToBreakerCallback(RequestStatusType.Failure);
                return RequestStatusType.Failure;
            }
        }
        // This method is called after each request to validate the response from the server to see if it was successful or not.
        public virtual async Task<RequestStatusType> ValidateSuccess(HttpResponseMessage resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                if ((await resp.Content.ReadAsStringAsync()).Contains("Error"))
                {
                    _logger.LogWarning("Failed Request through CircuitBreaker - Status 200 but string 'Error' was present in response");
                    return RequestStatusType.TransientFailure;
                }
                else
                {
                    _logger.LogInformation("Successful Request through CircuitBreaker - Status 200 string 'Error' not present in response");
                    return RequestStatusType.Success;
                }
            }
            _logger.LogWarning($"Failed Request through CircuitBreaker : Status {resp.StatusCode} was returned");            
            switch (resp.StatusCode)
            {
                case System.Net.HttpStatusCode.RequestTimeout: return RequestStatusType.TransientFailure;
                case System.Net.HttpStatusCode.TooManyRequests: return RequestStatusType.TransientFailure;
                default: return RequestStatusType.Failure;
            }
        }


    }
}