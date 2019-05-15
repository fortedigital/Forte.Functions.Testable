using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Forte.Functions.Testable.Core;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable
{
    public class InMemoryEntityContext : IDurableEntityContext
    {
        private readonly Queue<Operation> _queue = new Queue<Operation>();

        public async Task Run()
        {
            IsNewlyConstructed = true;

            while (true)
            {
                if (!_queue.TryDequeue(out var op))
                {
                    await Task.Delay(1);
                    continue;
                }

                await op.Execute(this, _client.FunctionsAssembly, _client.Services);

                IsNewlyConstructed = false;

                OperationExecuted?.Invoke(this, OperationName);

                Console.WriteLine("Executed " + OperationName);

                if (_destruct)
                    break;
            }

            _client._entities.Remove(Self);
            IsDestructed = true;
        }

        private bool _destruct;

        private event EventHandler<string> OperationExecuted;

        private object _state;
        private readonly object _input;
        private readonly InMemoryOrchestrationClient _client;
        private object _result;

        public InMemoryEntityContext(EntityId entityId, string operationName, object input, InMemoryOrchestrationClient client)
        {
            EntityName = entityId.EntityName;
            Key = entityId.EntityKey;
            Self = entityId;
            OperationName = operationName;
            _input = input;
            _client = client;
        }

        public T GetState<T>(Func<T> initializer = null)
        {
            if (null != initializer) _state = initializer();

            return null != _state 
                ? (T)_state
                : default;
        }

        private int _stateVersion = 0;

        public void SetState(object state)
        {
            _stateVersion++;
            _state = state;
        }

        public T GetInput<T>()
        {
            return null != _input
                ? (T)_input
                : default;
        }

        public object GetInput(Type inputType)
        {
            return _input;
        }

        public void Return(object result)
        {
            _result = result;
        }

        public void DestructOnExit()
        {
            _destruct = true;
        }

        public void SignalEntity(EntityId entity, string operationName, object operationInput = null)
        {
            _queue.Enqueue(new Operation(entity, operationName, operationInput));
        }

        public string EntityName { get; }
        public string Key { get; }
        public EntityId Self { get; }
        public string OperationName { get; private set; }
        public bool IsNewlyConstructed { get; private set; }
        public bool IsDestructed { get; private set; }


        public class Operation
        {
            private readonly EntityId _entity;
            private readonly string _operationName;
            private readonly object _operationInput;

            public Operation(EntityId entity, string operationName, object operationInput)
            {
                _entity = entity;
                _operationName = operationName;
                _operationInput = operationInput;
            }

            public Task Execute(InMemoryEntityContext context, Assembly assembly, IServiceProvider services)
            {
                context.OperationName = _operationName;

                var invoker = new Invoker(assembly, services);

                var operation = invoker.FindFunctionByName(_operationName);
                var parameters = invoker.ParametersForFunction(operation, context).ToArray();

                return invoker.InvokeFunction(operation, null, parameters);
            }
        }

        public Task WaitForDestruction(TimeSpan? timeout) => WaitFor(nameof(WaitForDestruction), e => e.IsDestructed, timeout);

        public Task WaitForEntityOperation(string operationName, TimeSpan? timeout)
        {
            bool operationExecuted = false;

            void OnOperationExecuted(object s, string executedOperationName)
            {
                operationExecuted = executedOperationName == operationName;
                OperationExecuted -= OnOperationExecuted;
            }

            OperationExecuted += OnOperationExecuted;

            return WaitFor(nameof(WaitForEntityOperation), e => operationExecuted, null);
        }
        public async Task<TResult> WaitForEntityOperation<TResult>(string operationName, TimeSpan? timeout)
        {
            await WaitForEntityOperation(operationName, timeout);
            return GetState<TResult>();
        }

        public Task WaitForNextStateChange(TimeSpan? timeout)
        {
            var versionWas = _stateVersion;
            return WaitFor(nameof(WaitForNextStateChange), e => e._stateVersion > versionWas, timeout);
        }

        public async Task WaitFor(string conditionName, Func<InMemoryEntityContext, bool> condition, TimeSpan? timeout)
        {
            var totalWait = TimeSpan.Zero;
            var maxWait = timeout ?? DefaultTimeout;
            TimeSpan wait = TimeSpan.FromMilliseconds(10);

            while (!condition(this))
            {
                await Task.Delay(10);
                totalWait = totalWait.Add(wait);

                if (totalWait > maxWait) throw new TimeoutException(
                    $"{conditionName} exceeded max wait time of {maxWait}");
            }
        }


        private TimeSpan DefaultTimeout => Debugger.IsAttached
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromSeconds(1);
    }
}