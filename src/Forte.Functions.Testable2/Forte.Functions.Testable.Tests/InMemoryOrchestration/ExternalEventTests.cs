using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class ExternalEventTests
    {
        [TestMethod]
        public async Task Can_raise_external_event()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEvent), null);

            await client.WaitForOrchestrationToExpectEvent(instanceId, Funcs.ExternalEventName);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

            await client.RaiseEventAsync(instanceId, Funcs.ExternalEventName);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status2 = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status2.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_timeout_external_event()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEventTimeout), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Failed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
        }
    }
}
