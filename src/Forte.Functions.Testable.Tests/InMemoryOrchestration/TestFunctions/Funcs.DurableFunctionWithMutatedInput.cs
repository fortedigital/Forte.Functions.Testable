using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithMutatedInput))]
        public static async Task DurableFunctionWithMutatedInput(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var inputBeforeMutation = context.GetInput<TestFunctionInput>();
            inputBeforeMutation.Token = "mutated orchestrator";

            var inputAfterMutation = context.GetInput<TestFunctionInput>();
            Assert.AreEqual("original", inputAfterMutation.Token, "Orchestrator should not mutate input source");

            await context.CallActivityAsync(nameof(AnActivityWithMutatedInput), inputAfterMutation);

            Assert.AreEqual("original", inputAfterMutation.Token, "Activity should not share orchestrator input source");
        }

        [FunctionName(nameof(AnActivityWithMutatedInput))]
        public static Task AnActivityWithMutatedInput([ActivityTrigger] DurableActivityContextBase context)
        {
            var inputBefore = context.GetInput<TestFunctionInput>();

            inputBefore.Token = "mutated activity context";

            var inputAfter = context.GetInput<TestFunctionInput>();
            Assert.AreEqual("original", inputAfter.Token, "Activity should not mutate input source");

            return Task.CompletedTask;
        }
    }
}
