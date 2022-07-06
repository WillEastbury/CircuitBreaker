using System;
using System.Threading.Tasks;
using CircuitBreaker.Core.Enums;

namespace CircuitBreaker.Core.Interfaces
{
    public interface IDynamicCircuitOperations
    {
        Func<string, string, Task<RequestStatusType>> ProcessDelegateOperationalMessage { get; }
        Func<Task<RequestStatusType>> ProcessDelegateSyntheticMessage { get; }
    }
}