using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

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
            var timeout = context.GetInput<TimeSpanInput>().TimeSpan;
            await context.WaitForExternalEvent(ExternalEventName, timeout);
        }

        [FunctionName(nameof(DurableFunctionWithExternalEventTimeoutCancel))]
        public static async Task DurableFunctionWithExternalEventTimeoutCancel(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var timeout = context.GetInput<TimeSpanInput>();
            try
            {
                await context.WaitForExternalEvent(ExternalEventName, timeout.TimeSpan, TimeSpanInput.Token);
            }
            catch(Exception ex)
            {
                
            }
        }
        public const string ExternalEventName = "ev1";
    }

    public class TimeSpanInput
    {
        public TimeSpan TimeSpan { get; set; }
        public static CancellationToken Token { get; set; }

        public static TimeSpanInput FromMilliseconds(int ms) => new TimeSpanInput {TimeSpan = TimeSpan.FromMilliseconds(ms)};
        public static TimeSpanInput FromMinutes(int minutes) => new TimeSpanInput { TimeSpan = TimeSpan.FromMinutes(minutes)};
    }
}