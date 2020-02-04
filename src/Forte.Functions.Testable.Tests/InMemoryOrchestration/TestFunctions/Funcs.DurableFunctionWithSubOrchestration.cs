using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithSubOrchestration))]
        public static Task DurableFunctionWithSubOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            return context.CallSubOrchestratorAsync(nameof(SubOrchestration), null);
        }

        [FunctionName(nameof(SubOrchestration))]
        public static Task SubOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            return context.CallActivityAsync(nameof(AnActivity), null);
        }
    }
}