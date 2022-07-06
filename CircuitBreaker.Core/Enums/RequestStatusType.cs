namespace CircuitBreaker.Core.Enums
{
    public enum RequestStatusType : int
    {
        NotSet = 0,
        Success = 1,
        Failure = 2,
        TransientFailure = 3
    }
}