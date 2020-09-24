using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public class DurableFuncWithCallHttp
    {
        [FunctionName(nameof(CallHttpFunction))]
        public async Task<DurableHttpResponse> CallHttpFunction(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            return await context.CallHttpAsync(HttpMethod.Get, new Uri("http://test.url"), "");
        }
    }
}