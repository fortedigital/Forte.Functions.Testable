using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class SubOrchestrationTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_start_sub_orchestration()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithSubOrchestration), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

    }

    [TestClass]
    public class StatusTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_get_null_for_unknown_instance_id()
        {

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);

            var status = await client.GetStatusAsync(Guid.NewGuid().ToString());

            Assert.IsNull(status);
        }
    }
}