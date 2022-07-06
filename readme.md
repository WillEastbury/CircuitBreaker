# How to Implement a Http Watchdog to circuit break over a Service Bus consumer when the target of the messages on the queue (an http endpoint) is down or degraded to stop messages being deadlettered. 

It appears that executing and trapping a service bus connection based on the availability of a downstream service is not quite as simple as I initially thought. 
Fear not though, as here is an extensible sample.

# This code is provided as-is with no warranty of any kind.

Sample Libraries, Host, host Config file and test target are provided in the repo.



The watchdog will kick in to poll the backend service when there are no messages on the bus arriving.

----------------------
If you want a more custom implementation, then inherit from our base classes, or override one of the following interfaces and pass your version into 

services.ConfigureIRequestProviderHostService<>()

- HttpCircuitOperations -- Where the actual requests are crafted through the circuit breaker (convenience to wrap the operations that execute through the breaker, it should be injected into the circuit breaker Instance of IWatchdogBreaker)
- HttpWatchDogPollingBreaker -- Runs the watchdog and handles management of the breaker's state
- RingCircuitResultStore -- Is a circular buffer of prior requests and their success or failure, this is used for the watchdog to see if the service is down.
- ServiceBusSessionProcessorService -- This implements the polling loop for service bus sessions and executes it's transactions pulled from the service bus when the breaker service is up.

To get the service working in any context you will have to inherit from the HttpCircuitOperations base class (If your command path is http(s) anyway) and override these two methods to shape the request, or Implement ICircuitOperations (You will have to do this if you are not sending the final requests over Http(s). 
---------------------------------------------------------

        public virtual async ValueTask<RequestStatusType> ProcessStandardOperationalMessageAsync(string message, string ExtraHeaderInfo)
        {
            // Decode the payload from the ingress service here and do something with it then simply return await FireRequestBehindBreakerAsync 
            return await FireRequestBehindBreakerAsync("OperationalClient", "/transaction/1234", HttpMethod.Post, message, ExtraHeaderInfo: ExtraHeaderInfo);
        }

        // This method is called when the circuit is open to pass synthetic transactions in much the same vein as above. simply return await FireRequestBehindBreakerAsync 
        public virtual async ValueTask<RequestStatusType> ProcessSyntheticTestMessageAsync()
        {
            return await FireRequestBehindBreakerAsync("WatchDogClient", "/test", HttpMethod.Get, "This is a synthetic message", ExtraHeaderInfo: "Test");
        }

You could also override the following method to shape a custom HttpRequestMessage. 

    public virtual async ValueTask<HttpRequestMessage> GetPreparedRequestAsync(string tailUri = "/", HttpMethod method = null, string UTF8ContentBody = "", string BearerToken = "", Func<string, Task<string>> getBearerTokenCallbackAsync = null, string ContentType = "application/json", string Accept = "application/json", string ExtraHeaderInfo = "")

In practice you won't need to do this often, as it's probably configurable enough already. 
For auth you can specify a token callback in getBearerTokenCallbackAsync to use a custom method to acquire a bearer token and attach it. Signature is Func<string, Task<string>> getBearerTokenCallbackAsync so just give it a method that accepts a string as input and returns a Task<string> to be awaited. If you already have a token, just pass it in via the BearerToken parameter and we will append 'Bearer ' to it. 

If you have some complex unusual success / failure logic, then you can override to additionally specify what you think is transient and what you think is an error.
public virtual async Task<RequestStatusType> ValidateSuccess(HttpResponseMessage resp)

You must return a RequestStatusType back for the breaker to know if the method executed successfully. 

Out of the box the default implementation will look inside the response body of successful requests for the word 'Error' and if that is there, will mark the request as being a transient error.
case System.Net.HttpStatusCode.RequestTimeout, and System.Net.HttpStatusCode.TooManyRequests will also return a transient error.
If the request returns success and doesn't have the word 'Error' in the payload, we return success. 
Anything else is a Failure Error. 
        

-------------------------------------

Http wise, you get three clients injected out of the box via HttpClientFactory that you can use with base URLs but others can happily be added via DI into your host.

These are :- 
- OperationalClient (BaseUrlSet to PollingUrlBase from the settings file)
- WatchDogClient (BaseUrlSet to OperationsUrlBase from the settings file)
- PlainOldClient (no base url set).

--------------------------    

Simples, have fun! and please raise Github Issues if you find anything broken or would like to chat. 

Thanks,
Will Eastbury

--------------------------
