using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithOneActivityAsync))]
        public static async Task DurableFunctionWithOneActivityAsync(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<TestFunctionInputAsync>();
            await context.CallActivityAsync(nameof(AnActivityAsync), input);
        }

        [FunctionName(nameof(AnActivityAsync))]
        public static Task AnActivityAsync([ActivityTrigger] IDurableActivityContext _)
        {
            return Task.CompletedTask;
        }

        [FunctionName(nameof(DurableFunctionWithOneActivityAsyncReturn))]
        public static async Task DurableFunctionWithOneActivityAsyncReturn(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<TestFunctionInputAsync>();
            var result = await context.CallActivityAsync<string>(nameof(AnActivityAsyncReturn), input);
            Assert.AreEqual("OK", result);
        }

        [FunctionName(nameof(AnActivityAsyncReturn))]
        public static Task<string> AnActivityAsyncReturn([ActivityTrigger] IDurableActivityContext _)
        {
            return Task.FromResult("OK");
        }
    }

    public class TestFunctionInputAsync
    {
        public string Token { get; set; }
    }
}
