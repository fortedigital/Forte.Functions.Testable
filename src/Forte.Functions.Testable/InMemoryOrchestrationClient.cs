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
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable
{
    public class InMemoryOrchestrationClient : DurableOrchestrationClientBase
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

        public override HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        public override HttpManagementPayload CreateHttpManagementPayload(string instanceId)
        {
            return new HttpManagementPayload();
        }

        public override async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
            HttpRequestMessage request, 
            string instanceId, 
            TimeSpan timeout,
            TimeSpan retryInterval)
        {
            try
            {
                await WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed, timeout);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch(TimeoutException)
            {
                return CreateCheckStatusResponse(request, instanceId);
            }
        }

        readonly Dictionary<string, InMemoryOrchestrationContext> _instances = new Dictionary<string, InMemoryOrchestrationContext>();

        public override async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object input)
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = "instance-" + _instances.Count.ToString();

            if (_instances.TryGetValue(instanceId, out var existing))
            {
                await this.TerminateAsync(instanceId, null);
            }

            var context = new InMemoryOrchestrationContext(this);
            _instances.Add(instanceId, context);

            context.Run(orchestratorFunctionName, input);

            return instanceId;
        }

        public override Task RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            return RaiseEventAsync(TaskHubName, instanceId, eventName, eventData);
        }

        public override Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData,
            string connectionName = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("RaiseEventAsync found no instance with id " + instanceId);

            context.NotifyExternalEvent(eventName, eventData);
            return Task.CompletedTask;
        }

        public override Task TerminateAsync(string instanceId, string reason)
        {
            _instances.Remove(instanceId); 
            return Task.CompletedTask;
        }

        public override Task RewindAsync(string instanceId, string reason)
        {
            throw new NotImplementedException();
        }

        public override Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput = true)
        {
            if (!_instances.TryGetValue(instanceId, out var context))
                return Task.FromResult<DurableOrchestrationStatus>(null);

            return Task.FromResult(ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context)));
        }

        public override Task<IList<DurableOrchestrationStatus>> GetStatusAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var list = _instances.Select(ToStatusObject).ToList();

            return Task.FromResult((IList<DurableOrchestrationStatus>)list);
        }

        private DurableOrchestrationStatus ToStatusObject(KeyValuePair<string, InMemoryOrchestrationContext> i)
        {
            return new DurableOrchestrationStatus
            {
                CreatedTime = i.Value.CreatedTime,
                CustomStatus = null == i.Value.CustomStatus 
                    ? null 
                    :  JToken.FromObject(i.Value.CustomStatus),
                History = JArray.FromObject(i.Value.History),
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

        public override Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var list = _instances.Where(i =>
                    i.Value.CreatedTime > createdTimeFrom && i.Value.CreatedTime < createdTimeTo &&
                    runtimeStatus.Contains(i.Value.Status))
                .Select(ToStatusObject)
                .ToList();


            return Task.FromResult((IList<DurableOrchestrationStatus>)list);
        }

        public override async Task<OrchestrationStatusQueryResult> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, int pageSize,
            string continuationToken, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId)
        {
            throw new NotImplementedException();
        }

        public override Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            throw new NotImplementedException();
        }

        public override string TaskHubName => "testhub";

        private TimeSpan DefaultTimeout => Debugger.IsAttached
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromSeconds(1);

        public async Task<DurableOrchestrationStatus> WaitForOrchestrationToReachStatus(string instanceId, OrchestrationRuntimeStatus desiredStatus, TimeSpan? timeout = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("WaitForOrchestrationToReachStatus found no instance with id " + instanceId);

            var totalWait = TimeSpan.Zero;
            var maxWait = timeout ?? DefaultTimeout;
            TimeSpan wait = TimeSpan.FromMilliseconds(10);

            while (context.Status != desiredStatus)
            {
                await Task.Delay(10);
                totalWait = totalWait.Add(wait);

                if (totalWait > maxWait) throw new TimeoutException("WaitForOrchestrationToReachStatus exceeded max wait time of " + maxWait);
            }

            return ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context));
        }

        public async Task WaitForOrchestrationToExpectEvent(string instanceId, string eventName, TimeSpan? timeout = null)
        {
            if(!_instances.TryGetValue(instanceId, out var context)) throw new Exception("WaitForOrchestrationToExpectEvent found no instance with id " + instanceId);

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
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("ChangeCurrentUtcTime found no instance with id " + instanceId);

            context.ChangeCurrentUtcTime(change);
        }
    }
}