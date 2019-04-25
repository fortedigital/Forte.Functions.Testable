using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class RetryActivityTests
    {
        [TestMethod]
        public async Task Can_succeed_after_retry()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithRetrySucceedingActivity), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed, TimeSpan.FromSeconds(5));

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_fail_after_max_retries()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithRetryFailingActivity), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Failed, TimeSpan.FromSeconds(5));

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
        }
    }
}