namespace CircuitBreaker.Options
{
    public class ServiceBusSessionProcessorServiceOptions
    {
        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
        public int MaxRetryCount { get; set; }
        public System.TimeSpan RetryDelay { get; set; }
        public int OverloadedDelayInMs {get;set;}
        public int MaxSessionsInParallel {get;set;}
        public int SessionTimeOutInSeconds { get; set; }
    }
}