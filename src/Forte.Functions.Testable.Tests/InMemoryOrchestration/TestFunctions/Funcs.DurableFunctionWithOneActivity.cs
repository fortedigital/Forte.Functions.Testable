using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithOneActivity))]
        public static async Task DurableFunctionWithOneActivity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.InstanceId);

            var input = context.GetInput<TestFunctionInput>();

            await context.CallActivityAsync(nameof(AnActivity), input);
        }

        [FunctionName(nameof(AnActivity))]
        public static void AnActivity([ActivityTrigger] IDurableActivityContext context)
        {
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.InstanceId);
        }

        [FunctionName(nameof(DurableFunctionWithOneActivityReturn))]
        public static async Task DurableFunctionWithOneActivityReturn(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var input = context.GetInput<TestFunctionInput>();
            var result = await context.CallActivityAsync<string>(nameof(AnActivityReturn), input);
            Assert.AreEqual("OK", result);
        }

        [FunctionName(nameof(AnActivityReturn))]
        public static string AnActivityReturn([ActivityTrigger] IDurableActivityContext context)
        {
            return "OK";
        }
    }

    public class TestFunctionInput
    {
        public string Token { get; set; }
    }
}
