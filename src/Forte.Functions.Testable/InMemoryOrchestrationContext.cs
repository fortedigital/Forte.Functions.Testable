using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;
using RetryOptions = Microsoft.Azure.WebJobs.Extensions.DurableTask.RetryOptions;

namespace Forte.Functions.Testable
{
    public class InMemoryOrchestrationContext : IDurableOrchestrationContext, IInMemoryContextInput
    {
        private readonly InMemoryOrchestrationClient _client;

        private readonly string _instanceId;
        private readonly string _parentInstanceId;
        internal string _orchestratorFunctionName;
        private JToken _serializedInput = null;
        private object _output = null;

        private DateTime _currentUtcDateTime = DateTime.UtcNow;

        public OrchestrationRuntimeStatus Status { get; private set; } = OrchestrationRuntimeStatus.Pending;
        public DateTime CreatedTime { get; private set; }
        public IList<HistoryEvent> History { get; } = new List<HistoryEvent>();
        public TEntityInterface CreateEntityProxy<TEntityInterface>(EntityId entityId)
        {
            throw new NotImplementedException();
        }

        public string Name => _orchestratorFunctionName;

        public InMemoryOrchestrationContext(string instanceId, string parentInstanceId, InMemoryOrchestrationClient client)
        {
            _client = client;
            _instanceId = instanceId;
            _parentInstanceId = parentInstanceId;
        }

        public async Task Run(string orchestratorFunctionName, object input)
        {
            _orchestratorFunctionName = orchestratorFunctionName;

            _serializedInput = input == null
                ? JValue.CreateNull()
                : JToken.FromObject(input);

            CreatedTime = CurrentUtcDateTime;
            Status = OrchestrationRuntimeStatus.Running;

            var executionStarted = new ExecutionStartedEvent(History.Count, JsonConvert.SerializeObject(input));
            History.Add(executionStarted);

            try
            {
                _output = await CallActivityFunctionByNameAsync(orchestratorFunctionName, input, reuseContext: true);

                Status = OrchestrationRuntimeStatus.Completed;

                History.Add(new ExecutionCompletedEvent(History.Count, JsonConvert.SerializeObject(_output), OrchestrationStatus.Completed));
            }
            catch (Exception ex)
            {   
                Status = OrchestrationRuntimeStatus.Failed;
                History.Add(new TaskFailedEvent(History.Count, 0, ex.Message, ex.StackTrace));
                History.Add(new ExecutionCompletedEvent(History.Count, null, OrchestrationStatus.Failed));
            }
        }

        TInput IDurableOrchestrationContext.GetInput<TInput>()
        {
            return  _serializedInput.ToObject<TInput>();
        }

        public void SetOutput(object output)
        {
            _output = output;
        }

        public void ContinueAsNew(object input, bool preserveUnprocessedEvents = false)
        {
            _customStatus = null;
            _externalEventTokens.Clear();

            this.Run(_orchestratorFunctionName, input);
        }

        private object _customStatus;

        public void SetCustomStatus(object customStatusObject)
        {
            _customStatus = customStatusObject;
        }

        public Task<DurableHttpResponse> CallHttpAsync(HttpMethod method, Uri uri, string content = null) =>
            CallHttpAsync(new DurableHttpRequest(method, uri, content: content));

        public Task<DurableHttpResponse> CallHttpAsync(DurableHttpRequest req)
        {
            var response = _client.CallHttpHandler?.Invoke(req) ?? new DurableHttpResponse(HttpStatusCode.OK);
            return Task.FromResult(response);
        }

        public Task<TResult> CallEntityAsync<TResult>(EntityId entityId, string operationName) => throw new NotImplementedException();

        public Task CallEntityAsync(EntityId entityId, string operationName) => throw new NotImplementedException();

        public Task<TResult> CallEntityAsync<TResult>(
            EntityId entityId,
            string operationName,
            object operationInput) => throw new NotImplementedException();

        public Task CallEntityAsync(EntityId entityId, string operationName, object operationInput) 
            => throw new NotImplementedException();

        public Task CallSubOrchestratorAsync(string functionName, object input) =>
            CallSubOrchestratorAsync<object>(functionName, null, input);

        public Task CallSubOrchestratorAsync(string functionName, string instanceId, object input)
            => CallSubOrchestratorAsync<object>(functionName, instanceId, input);

        public Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, object input) =>
            CallSubOrchestratorAsync<TResult>(functionName, null, input);

        public async Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            var createdEvent = new SubOrchestrationInstanceCreatedEvent(History.Count);
            History.Add(createdEvent);

            try
            {
                var subContext = new InMemoryOrchestrationContext(instanceId, _instanceId, _client);
                await subContext.Run(functionName, input);

                var result = (TResult)subContext._output;

                History.Add(new SubOrchestrationInstanceCompletedEvent(History.Count, createdEvent.EventId,
                    JsonConvert.SerializeObject(result)));

                return result;
            }
            catch (Exception ex)
            {
                History.Add(new SubOrchestrationInstanceFailedEvent(History.Count, createdEvent.EventId, ex.Message, ex.StackTrace));
                throw;
            }
        }

        public Task CallSubOrchestratorWithRetryAsync(
            string functionName,
            RetryOptions retryOptions,
            object input) => CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, null, input);

        public Task CallSubOrchestratorWithRetryAsync(
            string functionName,
            RetryOptions retryOptions,
            string instanceId,
            object input) => CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, instanceId, input);

        public Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input) =>
            CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, null, input);

        public Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId,
            object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, instanceId, input, firstAttempt, 1);
        }


        async Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await CallSubOrchestratorAsync<TResult>(functionName, instanceId, input);
            }
            catch (Exception ex)
            {
                var nextDelay = ComputeNextDelay(attempt, firstAttempt, ex, retryOptions);
                if (!nextDelay.HasValue) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.Value.TotalSeconds:0.###} seconds before retry attempt {attempt} for {functionName}"));

                if (nextDelay.Value > TimeSpan.Zero)
                {
                    await CreateTimer(CurrentUtcDateTime.Add(nextDelay.Value), (object)null, CancellationToken.None);
                }

                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }

        public Task CreateTimer(DateTime fireAt, CancellationToken cancelToken) => CreateTimer<object>(fireAt, null, CancellationToken.None);
        public async Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
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

        private readonly List<OrchestrationTimer> _activeTimers = new List<OrchestrationTimer>();

        public void Timeshift(TimeSpan change)
        {
            _currentUtcDateTime = _currentUtcDateTime.Add(change);

            for (var i = _activeTimers.Count - 1; i >= 0; i--)
            {
                var timer = _activeTimers[i];
                timer.TimeChanged(CurrentUtcDateTime);
            }
        }

        private TimeSpan DefaultTimeout => Debugger.IsAttached
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(5);

        public Task<T> WaitForExternalEvent<T>(string name)
        {
            return WaitForExternalEvent<T>(name, TimeSpan.FromDays(7), default, CancellationToken.None);
        }

        public Task WaitForExternalEvent(string name)
        {
            return WaitForExternalEvent<object>(name, TimeSpan.FromDays(7), default, CancellationToken.None);
        }

        public Task WaitForExternalEvent(string name, TimeSpan timeout, CancellationToken cancelToken = new CancellationToken())
        {
            return WaitForExternalEvent<object>(name, timeout, default, cancelToken);
        }

        public Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, CancellationToken cancelToken)
        {
            return WaitForExternalEvent<T>(name, timeout, default, cancelToken);
        }

        public async Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue, CancellationToken cancelToken)
        {
            History.Add(new GenericEvent(History.Count, $"Waiting for external event `{name}`"));

            var externalEventToken = new ExternalEventToken();
            _externalEventTokens[name] = externalEventToken;

            try
            {
                var ct = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, externalEventToken.Token).Token;

                await CreateTimer(CurrentUtcDateTime.Add(timeout), (object) null, ct);
                
                throw new TimeoutException(
                    $"WaitForExternalEvent timed out after {timeout.TotalSeconds:0.###} seconds without receiving external event `{name}`");
            }
            catch (TaskCanceledException e)
            {
                if (e.CancellationToken == cancelToken)
                {
                    throw;
                }
            }

            _externalEventTokens.Remove(name);

            if (null != externalEventToken.Value) return (T)externalEventToken.Value;
            return defaultValue;
        }

        readonly Dictionary<string, ExternalEventToken> _externalEventTokens = new Dictionary<string, ExternalEventToken>();

        class ExternalEventToken : CancellationTokenSource
        {
            public bool WasCancelled { get; private set; }

            public object Value { get; private set; }

            public void Notify(object value)
            {
                Value = value;
                Cancel(throwOnFirstException: false);
            }
        }
        public void NotifyExternalEvent(string name, object data)
        {
            if (!_externalEventTokens.TryGetValue(name, out var token)) return;
            History.Add(new GenericEvent(History.Count, $"External event `{name}` happened"));
            token.Notify(data);
        }

        public bool IsWaitingForExternalEvent(string name)
        {
            return _externalEventTokens.ContainsKey(name);
        }

        public Task<IDisposable> LockAsync(params EntityId[] entities)
        {
            throw new NotImplementedException();
        }

        public bool IsLocked(out IReadOnlyList<EntityId> ownedLocks)
        {
            throw new NotImplementedException();
        }

        public Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        public async Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
        {
            var taskScheduled = new TaskScheduledEvent(History.Count) { Name = functionName };
            History.Add(taskScheduled);

            try
            {
                var result = await CallActivityFunctionByNameAsync(functionName, input);

                History.Add(new TaskCompletedEvent(History.Count, taskScheduled.EventId,
                    JsonConvert.SerializeObject(result)));

                return result;
            }
            catch (Exception ex)
            {
                History.Add(new TaskFailedEvent(History.Count, taskScheduled.EventId, ex.Message, ex.StackTrace));
                throw;
            }
        }

        public Task CallActivityAsync(string functionName, object input) => CallActivityAsync<object>(functionName, input);

        public Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, Microsoft.Azure.WebJobs.Extensions.DurableTask.RetryOptions retryOptions, object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, 1);
        }

        public Task CallActivityWithRetryAsync(string functionName, RetryOptions retryOptions, object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallActivityWithRetryAsync<object>(functionName, retryOptions, input, firstAttempt, 1);
        }

        async Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await CallActivityAsync<TResult>(functionName, input);
            }
            catch (Exception ex)
            {
                var nextDelay = ComputeNextDelay(attempt, firstAttempt, ex, retryOptions);
                if (!nextDelay.HasValue) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.Value.TotalSeconds:0.###} seconds before retry attempt {attempt} for {functionName}"));

                if (nextDelay.Value > TimeSpan.Zero)
                {
                    await CreateTimer(CurrentUtcDateTime.Add(nextDelay.Value), (object)null, CancellationToken.None);
                }

                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }



        TimeSpan? ComputeNextDelay(int attempt, DateTime firstAttempt, Exception failure, RetryOptions retryOptions)
        {
            // adapted from
            // https://github.com/Azure/durabletask/blob/f9cc450539b5e37c97c19ae393d5bb1564fda7a8/src/DurableTask.Core/RetryInterceptor.cs

            if (attempt >= retryOptions.MaxNumberOfAttempts) return null;

            if (!retryOptions.Handle(failure)) return null;

            DateTime retryExpiration = (retryOptions.RetryTimeout != TimeSpan.MaxValue)
                ? firstAttempt.Add(retryOptions.RetryTimeout)
                : DateTime.MaxValue;

            if (CurrentUtcDateTime >= retryExpiration) return null;

            if (_client.UseDelaysForRetries) return TimeSpan.Zero;

            double nextDelayInMilliseconds = retryOptions.FirstRetryInterval.TotalMilliseconds *
                                             Math.Pow(retryOptions.BackoffCoefficient, attempt);
            TimeSpan? nextDelay = nextDelayInMilliseconds < retryOptions.MaxRetryInterval.TotalMilliseconds
                ? TimeSpan.FromMilliseconds(nextDelayInMilliseconds)
                : retryOptions.MaxRetryInterval;

            return nextDelay;
        }

        public void SignalEntity(EntityId entity, string operationName, object operationInput = null)
        {
            throw new NotImplementedException();
        }

        public void SignalEntity(EntityId entity, DateTime scheduledTimeUtc, string operationName, object operationInput = null)
        {
            throw new NotImplementedException();
        }

        public string StartNewOrchestration(string functionName, object input, string instanceId = null)
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = "imoc-instance_" + Guid.NewGuid();

            _client.StartNewAsync(functionName, instanceId, input);
            
            return instanceId;
        }

        public TEntityInterface CreateEntityProxy<TEntityInterface>(string entityKey)
        {
            throw new NotImplementedException();
        }

        public string InstanceId => _instanceId;
        public string ParentInstanceId => _parentInstanceId;
        public DateTime CurrentUtcDateTime => _currentUtcDateTime;
        public bool IsReplaying => false;
        public object CustomStatus => _customStatus;
        public JToken Input => _serializedInput;
        public object Output => _output;

        T IInMemoryContextInput.GetInput<T>()
        {
            return _serializedInput.ToObject<T>();
        }

        private async Task<dynamic> CallActivityFunctionByNameAsync(string functionName, object input, bool reuseContext = false)
        {
            var function = FindFunctionByName(functionName);

            if (null == function)
                throw new Exception($"Unable to find activity named {functionName} in {_client.FunctionsAssembly.FullName} (did you forget to add a FunctionName attribute to it)?");

            object instance = null;

            instance = function.IsStatic
                ? null
                : ActivatorUtilities.CreateInstance(_client.Services, function.DeclaringType);

            var context = reuseContext
                ? (IInMemoryContextInput)this
                : (IInMemoryContextInput)new InMemoryActivityContext(this, input);

            var parameters = ParametersForFunction(functionName, function, context).ToArray();

            var awaitableInfo = function.ReturnType.GetAwaitableInfo();

            if (awaitableInfo.IsAwaitable && awaitableInfo.ResultType == typeof(void))
            {
                await (dynamic)function.Invoke(instance, parameters);
                return default;
            }

            if (awaitableInfo.IsAwaitable)
            {
                return await (dynamic)function.Invoke(instance, parameters);
            }

            if (function.ReturnType == typeof(void))
            {
                function.Invoke(instance, parameters);
                return default;
            }

            return function.Invoke(instance, parameters);
        }


        private MethodInfo FindFunctionByName(string functionName)
        {
            return _client.FunctionsAssembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods())
                .FirstOrDefault(method => method.GetCustomAttribute<FunctionNameAttribute>()?.Name == functionName);
        }



        private IEnumerable<object> ParametersForFunction(string functionName, MethodInfo function, IInMemoryContextInput context)
        {
            foreach (var parameter in function.GetParameters())
            {
                if (typeof(IDurableOrchestrationContext).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return context;
                }
                else if (typeof(IDurableActivityContext).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return context;
                }
                else if (typeof(CancellationToken).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return CancellationToken.None;
                }
                else if (typeof(IDurableOrchestrationClient).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return _client;
                }
                else if (typeof(ILogger).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return _client.Services.GetService<ILoggerFactory>()?.CreateLogger(function.Name);
                }
                else if (typeof(ExecutionContext).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return new ExecutionContext
                    {
                        FunctionName = functionName,
                        InvocationId = Guid.NewGuid()
                    };
                }
                else if (parameter.GetCustomAttribute<ActivityTriggerAttribute>() != null)
                {
                    yield return typeof(IInMemoryContextInput)
                        .GetMethod(nameof(IInMemoryContextInput.GetInput))
                        ?.MakeGenericMethod(parameter.ParameterType)
                        .Invoke(context, new object[0]);
                }
                else
                {
                    yield return _client.Services.GetService(parameter.ParameterType);
                }
            }
        }
    }
}
