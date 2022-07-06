using System;
using System.Threading.Tasks;
using CircuitBreaker.Core.Enums;

namespace CircuitBreaker.Core.Interfaces
{
    public interface ICircuitResultStore
    {
        DateTime LastProcessedRequest { get; }
        ValueTask AddNewRequestStatusAsync(RequestStatusType requestStatus);
        int GetAllRequestCount();
        int GetStatsByRequestType(RequestStatusType rst);
    }
}