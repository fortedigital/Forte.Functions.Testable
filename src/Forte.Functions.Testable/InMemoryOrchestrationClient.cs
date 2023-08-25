using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable
{
    public class InMemoryOrchestrationClient : IDurableOrchestrationClient
    {
        public Assembly FunctionsAssembly { get; }
        public IServiceProvider Services { get; }

        /// <summary>
        /// Whether or not to respect delay time delay from RetryOptions when retrying activities. 
        /// When false, retries will happen immediately.
        /// Default is false.
        /// </summary>
        public bool UseDelaysForRetries { get; set; } = false;

        public InMemoryOrchestrationClient(Assembly functionsAssembly, IServiceProvider services)
        {
            FunctionsAssembly = functionsAssembly;
            Services = services;
        }

        public HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId,
            bool returnInternalServerErrorOnFailure = false)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        public IActionResult CreateCheckStatusResponse(HttpRequest request, string instanceId,
            bool returnInternalServerErrorOnFailure = false)
        {
            return new StatusCodeResult(200);
        }

        public HttpManagementPayload CreateHttpManagementPayload(string instanceId)
        {
            return new HttpManagementPayload();
        }

        public Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequestMessage request, string instanceId,
            TimeSpan? timeout = null, TimeSpan? retryInterval = null, bool returnInternalServerErrorOnFailure = false)
        {
            return WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, timeout, null);
        }

        public Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequest request, string instanceId, TimeSpan? timeout = null,
            TimeSpan? retryInterval = null, bool returnInternalServerErrorOnFailure = false)
        {
            return WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId, timeout, null);
        }

        readonly ConcurrentDictionary<string, InMemoryOrchestrationContext> _instances = new ConcurrentDictionary<string, InMemoryOrchestrationContext>();


        public async Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequest request, string instanceId, TimeSpan? timeout,
            TimeSpan? retryInterval)
        {
            try
            {
                await WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed, timeout);
                return new OkResult();
            }
            catch (TimeoutException)
            {
                return CreateCheckStatusResponse(request, instanceId);
            }
        }


        public Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId) => StartNewAsync<object>(orchestratorFunctionName, instanceId, null);

        public Task<string> StartNewAsync<T>(string orchestratorFunctionName, T input) where T : class => StartNewAsync<T>(orchestratorFunctionName, null, input);

        public async Task<string> StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input)
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = "instance-" + _instances.Count;

            if (_instances.TryGetValue(instanceId, out _))
            {
                await TerminateAsync(instanceId, null);
            }

            var context = new InMemoryOrchestrationContext(instanceId, null, this);
            _instances.TryAdd(instanceId, context);

            context.Run(orchestratorFunctionName, input);

            return instanceId;
        }

        public Task RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            return RaiseEventAsync(TaskHubName, instanceId, eventName, eventData);
        }

        public Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData,
            string connectionName = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("RaiseEventAsync found no instance with id " + instanceId);

            context.NotifyExternalEvent(eventName, eventData);
            return Task.CompletedTask;
        }

        public Task TerminateAsync(string instanceId, string reason)
        {
            _instances.Remove(instanceId, out var _);
            return Task.CompletedTask;
        }

        public Task SuspendAsync(string instanceId, string reason)
        {
            if (_instances.TryGetValue(instanceId, out var instance))
            {
                instance.Suspend(reason);
            }
            return Task.CompletedTask;
        }

        public Task ResumeAsync(string instanceId, string reason)
        {
            if (_instances.TryGetValue(instanceId, out var instance))
            {
                instance.Resume(reason);
            }
            return Task.CompletedTask;
        }

        public Task RewindAsync(string instanceId, string reason)
        {
            if (_instances.TryGetValue(instanceId, out var instance))
            {
                instance.Rewind(reason);
            }
            return Task.CompletedTask;
        }

        public Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory = true, bool showHistoryOutput = true, bool showInput = true)
        {
            if (!_instances.TryGetValue(instanceId, out var context))
                return Task.FromResult<DurableOrchestrationStatus>(null);

            return Task.FromResult(ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context)));

        }

        public Task<IList<DurableOrchestrationStatus>> GetStatusAsync(IEnumerable<string> instanceIds, bool showHistory = false, bool showHistoryOutput = false,
            bool showInput = false)
        {
            var statuses =
                instanceIds
                    .Select(instanceId => GetStatusAsync(instanceId, showHistory, showHistoryOutput, showInput))
                    .ToArray();
            Task.WaitAll(statuses);
            IList<DurableOrchestrationStatus> result = statuses.Select(status => status.Result).ToList();
            return Task.FromResult(result);
        }

        public Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime? createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var list = _instances.Where(i =>
                    i.Value.CreatedTime > createdTimeFrom && i.Value.CreatedTime < createdTimeTo &&
                    runtimeStatus.Contains(i.Value.Status))
                .Select(ToStatusObject)
                .ToList();


            return Task.FromResult((IList<DurableOrchestrationStatus>)list);
        }

        public Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId)
        {
            throw new NotImplementedException();
        }

        public Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(IEnumerable<string> instanceIds)
        {
            throw new NotImplementedException();
        }

        public Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            throw new NotImplementedException();
        }

        public Task<OrchestrationStatusQueryResult> GetStatusAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<OrchestrationStatusQueryResult> ListInstancesAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OrchestrationStatusQueryResult
            {
                DurableOrchestrationState = _instances.Select(ToStatusObject).ToArray()
            });
        }

        public async Task<string> RestartAsync(string instanceId, bool restartWithNewInstanceId = true)
        {
            if (!_instances.TryGetValue(instanceId, out var context))
            {
                throw new ArgumentException("No such instance");
            }

            await TerminateAsync(instanceId, "Restart");
            return await StartNewAsync(context._orchestratorFunctionName, instanceId, context.Input);

        }

        public Task MakeCurrentAppPrimaryAsync()
        {
            throw new NotImplementedException();
        }

        public string TaskHubName { get; } = "testhub";


        private TimeSpan DefaultTimeout => Debugger.IsAttached
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromSeconds(1);


        private DurableOrchestrationStatus ToStatusObject(KeyValuePair<string, InMemoryOrchestrationContext> i)
        {
            return new DurableOrchestrationStatus
            {
                CreatedTime = i.Value.CreatedTime,
                CustomStatus = null == i.Value.CustomStatus
                    ? null
                    : JToken.FromObject(i.Value.CustomStatus),
                History = JArray.FromObject(i.Value.History.ToArray()),
                Input = null == i.Value.Input
                    ? null
                    : JToken.FromObject(i.Value.Input),
                InstanceId = i.Key,
                LastUpdatedTime = i.Value.CurrentUtcDateTime,
                Name = i.Value.Name,
                RuntimeStatus = i.Value.Status,
                Output = null == i.Value.Output
                    ? null
                    : JToken.FromObject(i.Value.Output)
            };
        }

        public Task<DurableOrchestrationStatus> WaitForOrchestrationToReachStatus(string instanceId, OrchestrationRuntimeStatus desiredStatus, TimeSpan? timeout = null) => WaitForOrchestrationToReachStatus(instanceId, new[] {desiredStatus}, timeout);

        public async Task<DurableOrchestrationStatus> WaitForOrchestrationToReachStatus(string instanceId,
            OrchestrationRuntimeStatus[] desiredStatuses, TimeSpan? timeout = null)
        {

            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("WaitForOrchestrationToReachStatus found no instance with id " + instanceId);

            var totalWait = TimeSpan.Zero;
            var maxWait = timeout ?? DefaultTimeout;
            TimeSpan wait = TimeSpan.FromMilliseconds(10);

            while (!desiredStatuses.Contains(context.Status))
            {
                await Task.Delay(10);
                totalWait = totalWait.Add(wait);

                if (totalWait > maxWait) throw new TimeoutException("WaitForOrchestrationToReachStatus exceeded max wait time of " + maxWait);
            }

            return ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context));
        }

        /// <summary>
        /// Waits for the orchestration to finish either by completing, failing or being terminated.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public Task<DurableOrchestrationStatus> WaitForOrchestrationToFinish(string instanceId, TimeSpan? timeout = null) =>
            WaitForOrchestrationToReachStatus(instanceId,
                new[]
                {
                    OrchestrationRuntimeStatus.Completed, OrchestrationRuntimeStatus.Canceled,
                    OrchestrationRuntimeStatus.Failed, OrchestrationRuntimeStatus.Terminated
                }, timeout);


        /// <summary>
        /// Waits for the orchestration CustomStatus property to match a given predicate
        /// </summary>
        /// <typeparam name="TStatus">The expected type of CustomStatus</typeparam>
        /// <param name="instanceId">The orchestration to observe</param>
        /// <param name="predicate">The predicate to match</param>
        /// <param name="timeout">Timeout; if null, uses DefaultTimeout</param>
        /// <returns></returns>
        public async Task<DurableOrchestrationStatus> WaitForCustomStatus(string instanceId, Func<JToken, bool> predicate, TimeSpan? timeout = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("WaitForCustomStatus found no instance with id " + instanceId);

            var totalWait = TimeSpan.Zero;
            var maxWait = timeout ?? DefaultTimeout;
            TimeSpan wait = TimeSpan.FromMilliseconds(10);

            while (!predicate(context.CustomStatus == null ? null : JToken.FromObject(context.CustomStatus)))
            {
                await Task.Delay(10);
                totalWait = totalWait.Add(wait);

                if (totalWait > maxWait) throw new TimeoutException("WaitForCustomStatus exceeded max wait time of " + maxWait);
            }

            return ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context));
        }

        public async Task WaitForOrchestrationToExpectEvent(string instanceId, string eventName, TimeSpan? timeout = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("WaitForOrchestrationToExpectEvent found no instance with id " + instanceId);

            var totalWait = TimeSpan.Zero;
            var maxWait = timeout ?? DefaultTimeout;
            TimeSpan wait = TimeSpan.FromMilliseconds(10);

            while (!context.IsWaitingForExternalEvent(eventName))
            {
                await Task.Delay(wait);
                totalWait = totalWait.Add(wait);

                if (totalWait > maxWait) throw new TimeoutException("WaitForOrchestrationToExpectEvent exceeded max wait time of " + maxWait);
            }
        }

        public async Task Timeshift(string instanceId, TimeSpan change)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("Timeshift found no instance with id " + instanceId);

            context.Timeshift(change);
        }

        internal Func<DurableHttpRequest, DurableHttpResponse> CallHttpHandler;

        public void SetCallHttpHandler(Func<DurableHttpRequest, DurableHttpResponse> handler)
        {
            CallHttpHandler = handler;
        }
    }
}