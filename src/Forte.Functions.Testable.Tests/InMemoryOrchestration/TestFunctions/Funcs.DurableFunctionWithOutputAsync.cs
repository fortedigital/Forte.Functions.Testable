using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithOutput))]
        public static Task<string> DurableFunctionWithOutput(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            return Task.FromResult("OK");
        }

        [FunctionName(nameof(DurableFunctionWithSeparateActivityInput))]
        public static Task DurableFunctionWithSeparateActivityInput(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            return context.CallActivityAsync(nameof(ActivityVerifyingInput), new TestFunctionInput(){Token = "activity"});
        }

        [FunctionName(nameof(ActivityVerifyingInput))]
        public static Task ActivityVerifyingInput([ActivityTrigger] DurableActivityContextBase context)
        {
            var input = context.GetInput<TestFunctionInput>();
            Assert.AreEqual("activity", input.Token);
            return Task.CompletedTask;
        }
    }
}