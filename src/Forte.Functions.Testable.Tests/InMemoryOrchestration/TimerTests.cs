using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class TimerTests
    {
        [TestMethod]
        public async Task Can_fast_forward_time()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithTimer), new DurableFunctionWithTimerInput(TimeSpan.FromHours(1)));

            await client.Timeshift(instanceId, TimeSpan.FromHours(1));

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }
    }
}