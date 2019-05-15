using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable
{
    public class InMemoryActivityContext : IDurableActivityContext
    {
        private readonly IDurableOrchestrationContext _parentContext;
        private readonly object _input;

        public InMemoryActivityContext(IDurableOrchestrationContext parentContext, object input)
        {
            
            _parentContext = parentContext;
            _input = input;
        }

        public T GetInput<T>()
        {
            return (T)_input;
        }

        public string InstanceId => _parentContext.InstanceId;

        public Guid NewGuid()
        {
            return _parentContext.NewGuid();
        }

        public Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
        {
            return _parentContext.CallActivityAsync<TResult>(functionName, input);
        }

        public Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            return _parentContext.CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input);
        }

        public Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return _parentContext.CallSubOrchestratorAsync<TResult>(functionName, instanceId, input);
        }

        public Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId,
            object input)
        {
            return _parentContext.CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, instanceId, input);
        }

        public Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            return _parentContext.CreateTimer(fireAt, state, cancelToken);
        }

        public Task<T> WaitForExternalEvent<T>(string name)
        {
            return _parentContext.WaitForExternalEvent<T>(name);
        }

        public Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            return _parentContext.WaitForExternalEvent<T>(name, timeout);
        }

        public Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            return _parentContext.WaitForExternalEvent(name, timeout, defaultValue);
        }

        public void ContinueAsNew(object input)
        {
            _parentContext.ContinueAsNew(input);
        }

        public void SetCustomStatus(object customStatusObject)
        {
            _parentContext.SetCustomStatus(customStatusObject);
        }

        public DateTime CurrentUtcDateTime => _parentContext.CurrentUtcDateTime;
    }
}