namespace CircuitBreaker.Options
{
    public class RingCircuitResultStoreOptions
    {   public int SyntheticPulseOnIdleOrDownInSeconds { get; set; }
        public int MaxHistoryInRingBuffer { get; set; }
    }
}