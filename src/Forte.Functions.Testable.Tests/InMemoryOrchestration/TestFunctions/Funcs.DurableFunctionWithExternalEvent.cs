using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithExternalEvent))]
        public static async Task DurableFunctionWithExternalEvent(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.WaitForExternalEvent(ExternalEventName);
        }

        [FunctionName(nameof(DurableFunctionWithExternalEventTimeout))]
        public static async Task DurableFunctionWithExternalEventTimeout(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.WaitForExternalEvent(ExternalEventName, TimeSpan.FromMilliseconds(5));
        }

        public const string ExternalEventName = "ev1";
    }
}