using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithDirectBinding))]
        public static async Task DurableFunctionWithDirectBinding(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            await context.CallActivityAsync(nameof(AnActivityDirectBinding), new TestFunctionInput
            {
                Token = "token"
            });
        }

        [FunctionName(nameof(AnActivityDirectBinding))]
        public static Task AnActivityDirectBinding([ActivityTrigger] TestFunctionInput input)
        {
            Assert.AreEqual("token", input.Token);
            return Task.CompletedTask;
        }
    }
}
