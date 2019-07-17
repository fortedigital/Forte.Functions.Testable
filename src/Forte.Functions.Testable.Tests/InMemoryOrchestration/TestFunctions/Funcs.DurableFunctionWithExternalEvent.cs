using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithExternalEvent))]
        public static async Task DurableFunctionWithExternalEvent(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            await context.WaitForExternalEvent(ExternalEventName);
        }

        [FunctionName(nameof(DurableFunctionWithExternalEventTimeout))]
        public static async Task DurableFunctionWithExternalEventTimeout(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            var timeout = context.GetInput<TimeSpan>();
            await context.WaitForExternalEvent(ExternalEventName, timeout);
        }

        public const string ExternalEventName = "ev1";
    }
}