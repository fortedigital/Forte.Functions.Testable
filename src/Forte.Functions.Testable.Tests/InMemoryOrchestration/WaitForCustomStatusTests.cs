using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class WaitForCustomStatusTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_WaitForCustomStatus_to_match_predicate()
        {
            var input = new TestFunctionInputAsync();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithCustomStatus), input);

            var status = await client.WaitForCustomStatus(instanceId, c => c?.ToObject<CustomStatus>() != null);

            Assert.AreEqual(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

            Assert.IsNotNull(status.CustomStatus);
        }
    }
}