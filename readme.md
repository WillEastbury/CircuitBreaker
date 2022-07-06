# CircuitBreaker.Core

#### Http based Watchdog that circuit breaks over a Service Bus consumer when the target of the messages on the queue (an http endpoint) is down or degraded to stop messages being deadlettered. 

It appears that executing and trapping a service bus connection based on the availability of a downstream service is not quite as simple as I initially thought. 
Fear not though, as here is an extensible sample.

# This code is provided as-is with no warranty of any kind.

## Sample Libraries, Host, host Config file and test target are provided in the repo.

![Simple Architecture of Will Eastbury's CircuitBreaker](/simplediag.png)

The watchdog will kick in to poll the backend service when there are no messages on the bus arriving.

### TL;DR; You can find a simple implementation example with sample messages in the CircuitBreaker.Host/UsageSample folder

#### The sample messages can be found in the ServiceBusMessageSamples folder off the root and you can use the ServiceBus Explorer tool to send them, do not forget to set the content type properly to application/json.

#### You will of course need to rename appsetting_sample.json to appsettings.json and set the correct values for your base urls as well as provisioning a valid service bus connection string to connect to the bus. 

----------------------
### If you want a more custom implementation, then inherit from our base classes which provide a default implementation, or override one of the following interfaces and pass your version into the di startup method in.

services.ConfigureIRequestProviderHostService<>()

- CircuitBreaker.Http.HttpCircuitOperations -- Where the actual requests are crafted through the circuit breaker (convenience to wrap the operations that execute through the breaker, it should be injected into the circuit breaker Instance of IWatchdogBreaker)
- CircuitBreaker.Http.HttpWatchDogPollingBreaker -- Runs the watchdog and handles management of the breaker's state
- CircuitBreaker.Core.RingCircuitResultStore -- Is a circular buffer of prior requests and their success or failure, this is used for the watchdog to see if the service is down.
- CircuitBreaker.Azure.ServiceBus.ServiceBusSessionProcessorService -- This implements the polling loop for service bus sessions and executes it's transactions pulled from the service bus when the breaker service is up.

### To get the service working in any context you will have to inherit from the HttpCircuitOperations base class (If your command path is http(s) anyway) and override these two methods to shape the request, or Implement ICircuitOperations (You will have to do this if you are not sending the final requests over Http(s). 

The default behviour will simply send a ping message to /test on the WatchDogClient (see below) and will send to /Transaction/1234 on the OperationalClient and will forward any messages received from the bus directly to /transaction/1234. This is exactly how the DemoASPNETService app is configured to receive traffic. 

### Speaking of the test app. You can simulate failures or pressure by calling the /FailureSim/{ErrorChance}/{TransientChance} endpoint, specifying an integer percentage for error chance and Transient chance respectively, this will set the likelihood of random failures occurring in the service to test your circuit breaker. 
---------------------------------------------------------

## To derive from CircuitBreaker.Http.HttpCircuitOperations override the following methods:

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

### Out of the box the default implementation will look inside the response body of successful requests for the word 'Error' and if that is there, will mark the request as being a transient error.
case System.Net.HttpStatusCode.RequestTimeout, and System.Net.HttpStatusCode.TooManyRequests will also return a transient error.
If the request returns success and doesn't have the word 'Error' in the payload, we return success. 
Anything else is a Failure Error. 
        

-------------------------------------

### Http wise, you get three clients injected out of the box via HttpClientFactory that you can use with base URLs but others can happily be added via DI into your host.

These are :- 
- OperationalClient (BaseUrlSet to PollingUrlBase from the settings file)
- WatchDogClient (BaseUrlSet to OperationsUrlBase from the settings file)
- PlainOldClient (no base url set).

--------------------------    

Simples, have fun and please raise Github Issues if you find anything broken or would like to chat. 

Thanks for reading!
Will Eastbury

--------------------------
