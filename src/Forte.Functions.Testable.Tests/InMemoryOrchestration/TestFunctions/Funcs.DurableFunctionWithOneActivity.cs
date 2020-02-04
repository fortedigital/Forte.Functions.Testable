using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithOneActivity))]
        public static async Task DurableFunctionWithOneActivity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<TestFunctionInput>();
            await context.CallActivityAsync(nameof(AnActivity), input);
        }

        [FunctionName(nameof(AnActivity))]
        public static Task AnActivity([ActivityTrigger] IDurableActivityContext context)
        {
            return Task.CompletedTask;
        }
    }
    public class TestFunctionInput
    {
        public string Token { get; set; }
    }
}