using System;
using System.Net;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class CallHttpTests
    {

        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task CallHttpAsync_responds_OK_as_default()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(DurableFuncWithCallHttp.CallHttpFunction), new DurableFunctionWithTimerInput(TimeSpan.FromHours(1)));

            var status = await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            Assert.AreEqual(HttpStatusCode.OK, status.Output?.ToObject<DurableHttpResponse>()?.StatusCode);
        }

        [TestMethod]
        public async Task Can_customize_CallHttpAsync_response()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var customResponse = new DurableHttpResponse(HttpStatusCode.BadRequest);
            client.SetCallHttpHandler(_ => customResponse);
            
            var instanceId = await client
                .StartNewAsync(nameof(DurableFuncWithCallHttp.CallHttpFunction), new DurableFunctionWithTimerInput(TimeSpan.FromHours(1)));

            var status = await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            Assert.AreEqual(customResponse.StatusCode, status.Output?.ToObject<DurableHttpResponse>()?.StatusCode);
        }
    }
}