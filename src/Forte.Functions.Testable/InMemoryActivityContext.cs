using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable
{
    public class InMemoryActivityContext : DurableOrchestrationContextBase
    {
        private readonly DurableOrchestrationContextBase _parentContext;
        private readonly object _input;

        public InMemoryActivityContext(DurableOrchestrationContextBase parentContext, object input)
        {
            _parentContext = parentContext;
            _input = input;
        }

        public override T GetInput<T>()
        {
            return (T)_input;
        }

        public override Guid NewGuid()
        {
            return _parentContext.NewGuid();
        }

        public override Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
        {
            return _parentContext.CallActivityAsync<TResult>(functionName, input);
        }

        public override Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            return _parentContext.CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input);
        }

        public override Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return _parentContext.CallSubOrchestratorAsync<TResult>(functionName, instanceId, input);
        }

        public override Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId,
            object input)
        {
            return _parentContext.CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, instanceId, input);
        }

        public override Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            return _parentContext.CreateTimer(fireAt, state, cancelToken);
        }

        public override Task<T> WaitForExternalEvent<T>(string name)
        {
            return _parentContext.WaitForExternalEvent<T>(name);
        }

        public override Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            return _parentContext.WaitForExternalEvent<T>(name, timeout);
        }

        public override Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            return _parentContext.WaitForExternalEvent(name, timeout, defaultValue);
        }

        public override void ContinueAsNew(object input)
        {
            _parentContext.ContinueAsNew(input);
        }

        public override void SetCustomStatus(object customStatusObject)
        {
            _parentContext.SetCustomStatus(customStatusObject);
        }

        public override DateTime CurrentUtcDateTime => _parentContext.CurrentUtcDateTime;
    }
}