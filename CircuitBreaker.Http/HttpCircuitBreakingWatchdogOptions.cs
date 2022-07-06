namespace CircuitBreaker.Options
{
    public class HttpCircuitBreakingWatchdogOptions
    {
        public int PollingIntervalMs { get; set; }
        public string PollingUrlBase { get; set; }
        public string OperationsUrlBase { get; set; }
        public int MaxErrorsPercentage { get; set; }
        public int MaxErrorsTransientPercentage  { get; set; }
        public int SyntheticPulseOnIdleOrDownInSeconds { get; set; }
        public int MaxHistoryInRingBuffer { get; set; }
    }
}