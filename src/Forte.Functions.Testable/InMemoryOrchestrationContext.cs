using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using Forte.Functions.Testable.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RetryOptions = Microsoft.Azure.WebJobs.RetryOptions;

namespace Forte.Functions.Testable
{
    public class InMemoryOrchestrationContext : IDurableOrchestrationContext
    {
        private readonly InMemoryOrchestrationClient _client;
        private string _orchestratorFunctionName;
        public object Input { get; private set; }
        public object Output { get; private set; }

        public InMemoryOrchestrationContext(InMemoryOrchestrationClient client)
        {
            _client = client;
        }

        public async Task Run(string instanceId, string parentInstanceId, string orchestratorFunctionName, object input)
        {
            InstanceId = instanceId;
            ParentInstanceId = parentInstanceId;
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

        public  T GetInput<T>()
        {
            return (T)Input;
        }


        public bool IsLocked(out IReadOnlyList<EntityId> ownedLocks)
        {
            throw new NotImplementedException();
        }

        public  Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        public  async Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
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
            var invoker = new Invoker(_client.FunctionsAssembly, _client.Services);

            var function = invoker.FindFunctionByName(functionName);

            var instance = function.IsStatic
                ? null
                : ActivatorUtilities.CreateInstance(_client.Services, function.DeclaringType);

            var context = reuseContext
                ? this
                : (IDurableOrchestrationContext)new InMemoryActivityContext(this, input);

            var parameters = invoker.ParametersForFunction(function, context).ToArray();

            return await invoker.InvokeFunction(function, instance, parameters);
        }

        public  Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, 1);
        }

        public void SignalEntity(EntityId entity, string operationName, object operationInput = null)
        {
            _client.SignalEntityAsync(entity, operationName, operationInput);
        }

        public string StartNewOrchestration(string functionName, object input, string instanceId = null)
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = NewGuid().ToString();

            _client.StartNewAsync(functionName, instanceId, input);

            return instanceId;
        }

        async Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await CallActivityAsync<TResult>(functionName, input);
            }
            catch(Exception ex)
            {
                var nextDelay = DelayUtil.ComputeNextDelay(attempt, firstAttempt, ex, retryOptions, CurrentUtcDateTime, _client.UseDelaysForRetries);
                if (!nextDelay.HasValue) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.Value.TotalSeconds:0.###} seconds before retry attempt {attempt} for {functionName}"));

                if (nextDelay.Value > TimeSpan.Zero)
                {
                    await this.CreateTimer(CurrentUtcDateTime.Add(nextDelay.Value), CancellationToken.None);
                }

                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }

        public async Task<TResult> CallEntityAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            await _client.SignalEntityAsync(entityId, operationName, operationInput);

            return await _client.WaitForEntityOperation<TResult>(entityId, operationName);
        }

        public async Task CallEntityAsync(EntityId entityId, string operationName, object operationInput)
        {
            await _client.SignalEntityAsync(entityId, operationName, operationInput);
            await _client.WaitForEntityOperation(entityId, operationName);
        }

        public  async Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            var createdEvent = new SubOrchestrationInstanceCreatedEvent(History.Count);
            History.Add(createdEvent);

            try
            {
                var subContext = new InMemoryOrchestrationContext(_client);
                await subContext.Run(instanceId, InstanceId, functionName, input);

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

        public  Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId,
            object input)
        {
            var firstAttempt = CurrentUtcDateTime;
            return CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, 1);
        }

        async Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input, DateTime firstAttempt, int attempt)
        {
            try
            {
                return await this.CallSubOrchestratorAsync<TResult>(functionName, input);
            }
            catch (Exception ex)
            {
                var nextDelay = DelayUtil.ComputeNextDelay(attempt, firstAttempt, ex, retryOptions, CurrentUtcDateTime, _client.UseDelaysForRetries);
                if (!nextDelay.HasValue) throw;

                History.Add(new GenericEvent(History.Count, $"Delaying {nextDelay.Value.TotalSeconds:0.###} seconds before retry attempt {attempt} for {functionName}"));

                if(nextDelay.Value > TimeSpan.Zero)
                { 
                    await this.CreateTimer(CurrentUtcDateTime.Add(nextDelay.Value), CancellationToken.None);
                }

                return await CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input, firstAttempt, attempt + 1);
            }
        }

        private readonly List<OrchestrationTimer> _activeTimers = new List<OrchestrationTimer>();

        public  async Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
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

        public  Task<T> WaitForExternalEvent<T>(string name)
        {
            return WaitForExternalEvent<T>(name, DefaultTimeout);
        }

        public  Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            return WaitForExternalEvent<T>(name, timeout, default);
        }

        readonly Dictionary<string, ExternalEventToken> _externalEventTokens = new Dictionary<string, ExternalEventToken>();

        public  async Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            History.Add(new GenericEvent(History.Count, $"Waiting for external event `{name}`"));

            var externalEventToken = new ExternalEventToken();
            _externalEventTokens[name] = externalEventToken;

            try
            {
                await Task.Delay(timeout, externalEventToken.Token);
                throw new TimeoutException($"WaitForExternalEvent timed out after {timeout.TotalSeconds:0.###} seconds without receiving external event `{name}`");
            }
            catch (TaskCanceledException e)
            {
                if (e.CancellationToken != externalEventToken.Token) throw;
            }

            _externalEventTokens.Remove(name);

            if (null != externalEventToken.Value) return (T) externalEventToken.Value;
            return defaultValue;
        }

        public Task<IDisposable> LockAsync(params EntityId[] entities)
        {
            return Task.FromResult<IDisposable>(new EntityLock(entities));
        }

        public void NotifyExternalEvent(string name, object data)
        {
            if (_externalEventTokens.TryGetValue(name, out var token))
            {
                History.Add(new GenericEvent(History.Count, $"External event `{name}` happened"));
                token.Notify(data);

            }
        }

        public bool IsWaitingForExternalEvent(string name)
        {
            return _externalEventTokens.ContainsKey(name);
        }

        public  void ContinueAsNew(object input, bool preserveUnprocessedEvents = false)
        {
            CustomStatus = null;
            _externalEventTokens.Clear();

            this.Run(InstanceId, ParentInstanceId, _orchestratorFunctionName, input);
        }

        public object CustomStatus { get; private set; }

        public  void SetCustomStatus(object customStatusObject)
        {
            CustomStatus = customStatusObject;
        }

        public string InstanceId { get; private set; }
        public string ParentInstanceId { get; private set; }

        private DateTime _currentUtcDateTime = DateTime.UtcNow;

        public  DateTime CurrentUtcDateTime => _currentUtcDateTime;
        public bool IsReplaying => false;

        public OrchestrationRuntimeStatus Status { get; private set; } = OrchestrationRuntimeStatus.Pending;
        public DateTime CreatedTime { get; private set; }
        public IList<HistoryEvent> History { get; } = new List<HistoryEvent>();
    }
}