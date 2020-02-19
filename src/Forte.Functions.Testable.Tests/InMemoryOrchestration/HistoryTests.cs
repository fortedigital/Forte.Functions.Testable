using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using DurableTask.Core.History;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class HistoryTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_get_history_for_activity()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivity), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            AssertHistoryEventOrder(status, EventType.ExecutionStarted, EventType.TaskScheduled, EventType.TaskCompleted,
                EventType.ExecutionCompleted);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_get_history_for_waiting()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEvent), null);

            await client.WaitForOrchestrationToExpectEvent(instanceId, Funcs.ExternalEventName);
            await client.RaiseEventAsync(instanceId, Funcs.ExternalEventName);
            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            AssertHistoryEventOrder(status, EventType.ExecutionStarted, EventType.GenericEvent, EventType.TimerCreated, EventType.GenericEvent,
                EventType.ExecutionCompleted);


            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        private void AssertHistoryEventOrder(DurableOrchestrationStatus status, params EventType[] eventOrder)
        {
            TestUtil.LogHistory(status, Console.Out);

            var history = status.History.ToObject<List<GenericHistoryEvent>>();

            for (int i = 0; i < eventOrder.Length; i++)
            {
                Assert.AreEqual(eventOrder[i], history[i].EventType);
            }
        }
    }

    public class GenericHistoryEvent
    {
        public string Data { get; set; }

        public EventType EventType { get; set; }
    }

}