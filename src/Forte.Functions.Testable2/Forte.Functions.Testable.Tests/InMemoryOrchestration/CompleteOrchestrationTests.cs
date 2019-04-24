using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class CompleteOrchestrationTests
    {
        [TestMethod]
        public async Task Can_execute_durable_function()
        {
            var input = new TestFunctionInput();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivity), input);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_execute_durable_function_with_output()
        {
            var input = new TestFunctionInput();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOutput), input);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
            Assert.AreEqual(JToken.FromObject("OK"), status.Output);
        }
    }
}