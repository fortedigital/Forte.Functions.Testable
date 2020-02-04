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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
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

        public  HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        public  HttpManagementPayload CreateHttpManagementPayload(string instanceId)
        {
            return new HttpManagementPayload();
        }

        public  async Task<HttpResponseMessage> WaitForCompletionOrCreateCheckStatusResponseAsync(
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

        public Task<string> StartNewAsync<T>(string orchestratorFunctionName, string instanceId, T input)
        {
            if (string.IsNullOrEmpty(instanceId)) instanceId = "instance-" + _instances.Count.ToString();
            var context = new InMemoryOrchestrationContext(this);

            if (_instances.TryGetValue(instanceId, out var existingInstance))
            {   
                TerminateAsync(instanceId, "Instance replaced via StartNewAsync");
            }

            _instances.Add(instanceId, context);

            context.Run(instanceId, null, orchestratorFunctionName, input);

            return Task.FromResult(instanceId);
        }

        public  Task RaiseEventAsync(string instanceId, string eventName, object eventData)
        {
            return RaiseEventAsync(TaskHubName, instanceId, eventName, eventData);
        }

        public  Task RaiseEventAsync(string taskHubName, string instanceId, string eventName, object eventData,
            string connectionName = null)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("RaiseEventAsync found no instance with id " + instanceId);

            context.NotifyExternalEvent(eventName, eventData);
            return Task.CompletedTask;
        }

        public async Task SignalEntityAsync(EntityId entityId, string operationName, object operationInput = null, string taskHubName = null,
            string connectionName = null)
        {
            if (!_entities.TryGetValue(entityId, out var entityContext))
            {
                entityContext = new InMemoryEntityContext(entityId, null, null, this);
                entityContext.Run();

                _entities.Add(entityId, entityContext);
            }

            entityContext.SignalEntity(entityId, operationName, operationInput);
        }

        internal readonly Dictionary<EntityId, InMemoryEntityContext> _entities = new Dictionary<EntityId, InMemoryEntityContext>();

        public Task<EntityStateResponse<T>> ReadEntityStateAsync<T>(EntityId entityId, string taskHubName = null, string connectionName = null)
        {
            var entityExists = _entities.TryGetValue(entityId, out var entityContext);

            return Task.FromResult(new EntityStateResponse<T>
            {
                EntityExists = entityExists,
                EntityState = entityExists ? entityContext.GetState<T>() : default
            });
        }

        public  Task TerminateAsync(string instanceId, string reason)
        {
            _instances.Remove(instanceId);
            return Task.CompletedTask;
        }

        public  Task RewindAsync(string instanceId, string reason)
        {
            throw new NotImplementedException();
        }

        public  Task<DurableOrchestrationStatus> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput = true)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("GetStatusAsync found no instance with id " + instanceId);

            return Task.FromResult(ToStatusObject(new KeyValuePair<string, InMemoryOrchestrationContext>(instanceId, context)));
        }

        public  Task<IList<DurableOrchestrationStatus>> GetStatusAsync(CancellationToken cancellationToken = new CancellationToken())
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

        public  Task<IList<DurableOrchestrationStatus>> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var list = _instances.Where(i =>
                    i.Value.CreatedTime > createdTimeFrom && i.Value.CreatedTime < createdTimeTo &&
                    runtimeStatus.Contains(i.Value.Status))
                .Select(ToStatusObject)
                .ToList();


            return Task.FromResult((IList<DurableOrchestrationStatus>)list);
        }

        public  Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId)
        {
            throw new NotImplementedException();
        }

        public  Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationStatus> runtimeStatus)
        {
            throw new NotImplementedException();
        }

        public Task<OrchestrationStatusQueryResult> GetStatusAsync(OrchestrationStatusQueryCondition condition, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public  Task<OrchestrationStatusQueryResult> GetStatusAsync(DateTime createdTimeFrom, DateTime? createdTimeTo, IEnumerable<OrchestrationRuntimeStatus> runtimeStatus, int pageSize,
            string continuationToken, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public  string TaskHubName => "testhub";

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

                if (totalWait > maxWait) throw new TimeoutException(
                    $"WaitForOrchestrationToReachStatus {desiredStatus} exceeded max wait time of {maxWait} while status was {context.Status}");
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

                if (totalWait > maxWait) throw new TimeoutException($"WaitForOrchestrationToExpectEvent '{eventName}' exceeded max wait time of {maxWait} while status was {context.Status}");
            }
        }

        public async Task Timeshift(string instanceId, TimeSpan change)
        {
            if (!_instances.TryGetValue(instanceId, out var context)) throw new Exception("ChangeCurrentUtcTime found no instance with id " + instanceId);

            context.ChangeCurrentUtcTime(change);
        }

        public Task WaitForEntityOperation(EntityId entityId, string operationName, TimeSpan? timeout = null)
        {
            if (!_entities.TryGetValue(entityId, out var entity)) throw new Exception("WaitForEntityDestruction found no entity with id " + entityId);

            return entity.WaitForEntityOperation(operationName, timeout);
        }

        public Task<TResult> WaitForEntityOperation<TResult>(EntityId entityId, string operationName, TimeSpan? timeout = null)
        {
            if (!_entities.TryGetValue(entityId, out var entity)) throw new Exception("WaitForEntityDestruction found no entity with id " + entityId);

            return entity.WaitForEntityOperation<TResult>(operationName, timeout);
        }


        public Task WaitForEntityDestruction(EntityId entityId, TimeSpan? timeout = null)
        {
            if (!_entities.TryGetValue(entityId, out var entity)) throw new Exception("WaitForEntityDestruction found no entity with id " + entityId);

            return entity.WaitForDestruction(timeout);
        }


        public Task WaitForNextStateChange(EntityId entityId, TimeSpan? timeout = null)
        {
            if (!_entities.TryGetValue(entityId, out var entity)) throw new Exception("WaitForEntityDestruction found no entity with id " + entityId);

            return entity.WaitForNextStateChange(timeout);
        }

        public HttpResponseMessage CreateCheckStatusResponse(HttpRequestMessage request, string instanceId, bool returnInternalServerErrorOnFailure = false)
        {
            throw new NotImplementedException();
        }

        public IActionResult CreateCheckStatusResponse(HttpRequest request, string instanceId, bool returnInternalServerErrorOnFailure = false)
        {
            throw new NotImplementedException();
        }

        public Task<IActionResult> WaitForCompletionOrCreateCheckStatusResponseAsync(HttpRequest request, string instanceId, TimeSpan timeout, TimeSpan retryInterval)
        {
            throw new NotImplementedException();
        }
    }
}