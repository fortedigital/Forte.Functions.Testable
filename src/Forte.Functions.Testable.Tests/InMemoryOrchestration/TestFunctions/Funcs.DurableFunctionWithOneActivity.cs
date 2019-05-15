using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

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
        public static Task AnActivity([ActivityTrigger] IDurableOrchestrationContext context)
        {
            return Task.CompletedTask;
        }
    }
    public class TestFunctionInput
    {
        public string Token { get; set; }
    }
}