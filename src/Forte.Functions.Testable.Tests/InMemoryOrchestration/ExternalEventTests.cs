using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class ExternalEventTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_raise_external_event()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEvent), null);

            await client.WaitForOrchestrationToExpectEvent(instanceId, Funcs.ExternalEventName);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

            await client.RaiseEventAsync(instanceId, Funcs.ExternalEventName);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status2 = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status2, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status2.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_timeout_external_event()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEventTimeout), TimeSpan.FromMilliseconds(5));

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Failed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_timeshift_external_event_timeout()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithExternalEventTimeout), TimeSpan.FromMinutes(1));

            await client.Timeshift(instanceId, TimeSpan.FromMinutes(2));

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Failed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
        }
    }
}
