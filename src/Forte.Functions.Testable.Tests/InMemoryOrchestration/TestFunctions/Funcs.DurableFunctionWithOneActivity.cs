using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithOneActivity))]
        public static async Task DurableFunctionWithOneActivity(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var input = context.GetInput<TestFunctionInput>();
            await context.CallActivityAsync(nameof(AnActivity), input);
        }

        [FunctionName(nameof(AnActivity))]
        public static void AnActivity([ActivityTrigger] DurableActivityContextBase context)
        {
        }

        [FunctionName(nameof(DurableFunctionWithOneActivityReturn))]
        public static async Task DurableFunctionWithOneActivityReturn(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var input = context.GetInput<TestFunctionInput>();
            var result = await context.CallActivityAsync<string>(nameof(AnActivityReturn), input);
            Assert.AreEqual("OK", result);
        }

        [FunctionName(nameof(AnActivityReturn))]
        public static string AnActivityReturn([ActivityTrigger] DurableActivityContextBase context)
        {
            return "OK";
        }
    }

    public class TestFunctionInput
    {
        public string Token { get; set; }
    }
}
