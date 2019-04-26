using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using RetryOptions = Microsoft.Azure.WebJobs.RetryOptions;

namespace Forte.Functions.Testable
{
    public class InMemoryOrchestrationContext : DurableOrchestrationContextBase
    {
        private readonly Assembly _functionsAssembly;
        private string _orchestratorFunctionName;
        public object Input { get; private set; }
        public object Output { get; private set; }

        public InMemoryOrchestrationContext(Assembly functionsAssembly)
        {
            _functionsAssembly = functionsAssembly;
        }

        public async Task Run(string orchestratorFunctionName, object input)
        {
            _orchestratorFunctionName = orchestratorFunctionName;
            Input = input;
            Output = null;
            CreatedTime = CurrentUtcDateTime;
            Status = OrchestrationRuntimeStatus.Running;

            var executionStarted = new ExecutionStartedEvent(History.Count, JsonConvert.SerializeObject(input));
            History.Add(executionStarted);

            try
            {
                Output = await CallActivityFunctionByNameAsync(orchestratorFunctionName, input, reuseContext:true);
            
                Status = OrchestrationRuntimeStatus.Completed;

                History.Add(new ExecutionCompletedEvent(History.Count, JsonConvert.SerializeObject(Output), OrchestrationStatus.Completed));
            }
            catch(Exception ex)
            {
                Status = OrchestrationRuntimeStatus.Failed;
                History.Add(new ExecutionCompletedEvent(History.Count, JsonConvert.SerializeObject(ex), OrchestrationStatus.Failed));
            }
        }

        public override T GetInput<T>()
        {
            return (T)Input;
        }

        public override Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        public override async Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
        {
            var taskScheduled = new TaskScheduledEvent(History.Count) {Name = functionName};
            History.Add(taskScheduled);

            try
            {
                var result = await CallActivityFunctionByNameAsync(functionName, input);

                History.Add(new TaskCompletedEvent(History.Count, taskScheduled.EventId,
                    JsonConvert.SerializeObject(result)));

                return result;
            }
            catch(Exception ex)
            {
                History.Add(new TaskFailedEvent(History.Count, taskScheduled.EventId, ex.Message, ex.StackTrace));
                throw;
            }
        }

        private async Task<dynamic> CallActivityFunctionByNameAsync(string functionName, object input, bool reuseContext = false)
        {
            var function = FindFunctionByName(functionName);

            var context = reuseContext
                ? this
                : (DurableOrchestrationContextBase)new InMemoryActivityContext(this, input);

            if (function.ReturnType.IsGenericType)
            {
                return await (dynamic) function.Invoke(null, new object[] {context});
            }
            else
            {
                await (dynamic) function.Invoke(null, new object[] {context});
                return default;
            }
        }

        public override Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, 1);
        }

        async Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await CallActivityAsync<TResult>(functionName, input);
            }
            catch(Exception ex)
            {
                var nextDelay = ComputeNextDelay(attempt, firstAttempt, ex, retryOptions);
                if (nextDelay == TimeSpan.Zero) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.TotalSeconds:##,##} seconds before retry attempt {attempt} for {functionName}"));

                await Task.Delay(nextDelay);
                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }

        TimeSpan ComputeNextDelay(int attempt, DateTime firstAttempt, Exception failure, RetryOptions retryOptions)
        {
            // adapted from 
            // https://github.com/Azure/durabletask/blob/f9cc450539b5e37c97c19ae393d5bb1564fda7a8/src/DurableTask.Core/RetryInterceptor.cs
            TimeSpan nextDelay = TimeSpan.Zero;

            if (attempt >= retryOptions.MaxNumberOfAttempts) return nextDelay;

            if (!retryOptions.Handle(failure)) return nextDelay;

            DateTime retryExpiration = (retryOptions.RetryTimeout != TimeSpan.MaxValue)
                ? firstAttempt.Add(retryOptions.RetryTimeout)
                : DateTime.MaxValue;

            if (CurrentUtcDateTime >= retryExpiration) return nextDelay;

            double nextDelayInMilliseconds = retryOptions.FirstRetryInterval.TotalMilliseconds *
                                             Math.Pow(retryOptions.BackoffCoefficient, attempt);
            nextDelay = nextDelayInMilliseconds < retryOptions.MaxRetryInterval.TotalMilliseconds
                ? TimeSpan.FromMilliseconds(nextDelayInMilliseconds)
                : retryOptions.MaxRetryInterval;

            return nextDelay;
        }

        private MethodInfo FindFunctionByName(string functionName)
        {
            return _functionsAssembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                .FirstOrDefault(method => method.GetCustomAttribute<FunctionNameAttribute>()?.Name == functionName);
        }

        public override async Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            var createdEvent = new SubOrchestrationInstanceCreatedEvent(History.Count);
            History.Add(createdEvent);

            try
            {
                var subContext = new InMemoryOrchestrationContext(_functionsAssembly);
                await subContext.Run(functionName, input);

                var result = (TResult)subContext.Output;

                History.Add(new SubOrchestrationInstanceCompletedEvent(History.Count, createdEvent.EventId, 
                    JsonConvert.SerializeObject(result)));

                return result;
            }
            catch(Exception ex)
            {
                History.Add(new SubOrchestrationInstanceFailedEvent(History.Count, createdEvent.EventId, ex.Message, ex.StackTrace));
                throw;
            }
        }

        public override Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId,
            object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, 1);
        }

        async Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await CallSubOrchestratorAsync<TResult>(functionName, input);
            }
            catch (Exception ex)
            {
                var nextDelay = ComputeNextDelay(attempt, firstAttempt, ex, retryOptions);
                if (nextDelay == TimeSpan.Zero) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.TotalSeconds:##,##} seconds before retry attempt {attempt} for {functionName}"));

                await Task.Delay(nextDelay);
                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }

        private readonly List<OrchestrationTimer> _activeTimers = new List<OrchestrationTimer>();

        public override async Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            var timerCreated = new TimerCreatedEvent(History.Count);
            History.Add(timerCreated);

            var timer = new OrchestrationTimer(fireAt, CurrentUtcDateTime, cancelToken);

            try
            {
                _activeTimers.Add(timer);
                await timer.Wait();
                History.Add(new TimerFiredEvent(timerCreated.EventId));
            }
            finally
            {
                _activeTimers.Remove(timer);
            }

            return state;
        }

        private class OrchestrationTimer : CancellationTokenSource
        {
            private DateTime StartedAt { get; }
            private DateTime FireAt { get; }
            public OrchestrationTimer(DateTime fireAt, DateTime startedAt, CancellationToken cancelToken)
            {
                StartedAt = startedAt;
                FireAt = fireAt;

                cancelToken.Register(Cancel);
            }

            public async Task Wait()
            {
                try
                {
                    var delay = FireAt - StartedAt;
                    await Task.Delay(delay, this.Token);
                }
                catch (TaskCanceledException)
                {
                    if (!_swallowTaskCancelledException) throw;
                }
            }

            private bool _swallowTaskCancelledException = false;

            public void TimeChanged(DateTime newTime)
            {
                if (newTime < FireAt) return;

                _swallowTaskCancelledException = true;
                Cancel();
            }
        }

        public void ChangeCurrentUtcTime(TimeSpan change)
        {
            _currentUtcDateTime = _currentUtcDateTime.Add(change);

            for(var i = _activeTimers.Count-1; i >= 0; i--)
            {
                var timer = _activeTimers[i];
                timer.TimeChanged(CurrentUtcDateTime);
            }
        }

        private TimeSpan DefaultTimeout => Debugger.IsAttached
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(5);

        public override Task<T> WaitForExternalEvent<T>(string name)
        {
            return WaitForExternalEvent<T>(name, DefaultTimeout);
        }

        public override Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            return WaitForExternalEvent<T>(name, timeout, default);
        }

        readonly Dictionary<string, ExternalEventToken> _externalEventTokens = new Dictionary<string, ExternalEventToken>();

        public override async Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            History.Add(new GenericEvent(History.Count, $"Waiting for external event `{name}`"));

            var externalEventToken = new ExternalEventToken();
            _externalEventTokens[name] = externalEventToken;

            try
            {
                await Task.Delay(timeout, externalEventToken.Token);
                throw new TimeoutException($"WaitForExternalEvent timed out after {timeout.TotalSeconds:##.##} seconds without receiving external event `{name}`");
            }
            catch (TaskCanceledException e)
            {
                if (e.CancellationToken != externalEventToken.Token) throw;
            }

            _externalEventTokens.Remove(name);

            if (null != externalEventToken.Value) return (T) externalEventToken.Value;
            return defaultValue;
        }

        public void NotifyExternalEvent(string name, object data)
        {
            if (_externalEventTokens.TryGetValue(name, out var token)) { 
                token.Notify(data);

                History.Add(new GenericEvent(History.Count, $"External event `{name}` raised"));
            }
        }

        public bool IsWaitingForExternalEvent(string name)
        {
            return _externalEventTokens.ContainsKey(name);
        }

        class ExternalEventToken : CancellationTokenSource
        {
            public ExternalEventToken()
            {
            }

            public object Value { get; private set; }

            public void Notify(object value)
            {
                Value = value;
                Cancel(throwOnFirstException: false);
            }
        } 

        public override void ContinueAsNew(object input)
        {
            CustomStatus = null;
            _externalEventTokens.Clear();

            this.Run(_orchestratorFunctionName, input);
        }

        public object CustomStatus { get; private set; }

        public override void SetCustomStatus(object customStatusObject)
        {
            CustomStatus = customStatusObject;
        }

        private DateTime _currentUtcDateTime = DateTime.UtcNow;

        public override DateTime CurrentUtcDateTime => _currentUtcDateTime;

        public OrchestrationRuntimeStatus Status { get; private set; } = OrchestrationRuntimeStatus.Pending;
        public DateTime CreatedTime { get; private set; }
        public IList<HistoryEvent> History { get; } = new List<HistoryEvent>();
    }
}