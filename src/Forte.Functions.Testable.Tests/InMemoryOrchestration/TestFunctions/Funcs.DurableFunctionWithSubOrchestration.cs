using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithSubOrchestration))]
        public static Task DurableFunctionWithSubOrchestration(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            return context.CallSubOrchestratorAsync(nameof(SubOrchestration), null);
        }

        [FunctionName(nameof(SubOrchestration))]
        public static Task SubOrchestration(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            return context.CallActivityAsync(nameof(AnActivityAsync), null);
        }
    }
}