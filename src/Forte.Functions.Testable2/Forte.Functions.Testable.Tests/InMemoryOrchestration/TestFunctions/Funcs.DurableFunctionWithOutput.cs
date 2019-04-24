using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

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
    }
}