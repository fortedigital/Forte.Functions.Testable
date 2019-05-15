using System;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithRetrySucceedingActivity))]
        public static async Task DurableFunctionWithRetrySucceedingActivity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityWithRetryAsync(
                nameof(FailAtGivenCallNoActivity), 
                new RetryOptions(TimeSpan.FromMilliseconds(1),2){}, 
                new RetryingActivitySetup(2));
        }

        [FunctionName(nameof(DurableFunctionWithRetryFailingActivity))]
        public static async Task DurableFunctionWithRetryFailingActivity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityWithRetryAsync(
                nameof(FailAtGivenCallNoActivity),
                new RetryOptions(TimeSpan.FromMilliseconds(1), 2) { },
                new RetryingActivitySetup(-1));
        }

        [FunctionName(nameof(FailAtGivenCallNoActivity))]
        public static Task FailAtGivenCallNoActivity([ActivityTrigger] IDurableOrchestrationContext context)
        {
            var activitySetup = context.GetInput<RetryingActivitySetup>();
            activitySetup.Increment();

            if(activitySetup.ShouldSucceed()) return Task.CompletedTask;

            throw new Exception("Failing call no " + activitySetup.CallNo);
        }
    }

    public class RetryingActivitySetup
    {
        private int SucceedAtCallNo { get; }
        public int CallNo { get; private set; }

        public RetryingActivitySetup(int succeedAtCallNo)
        {
            SucceedAtCallNo = succeedAtCallNo;
        }

        public void Increment()
        {
            CallNo++;
        }

        public bool ShouldSucceed() => CallNo == SucceedAtCallNo;
    }
}