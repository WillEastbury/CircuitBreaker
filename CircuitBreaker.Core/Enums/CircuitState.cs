namespace CircuitBreaker.Core.Enums
{
    public enum CircuitState : int 
    { 
        OK = -1,
        Unknown = 0,
        InTrouble = 1,
        Dead = 2
    }
}