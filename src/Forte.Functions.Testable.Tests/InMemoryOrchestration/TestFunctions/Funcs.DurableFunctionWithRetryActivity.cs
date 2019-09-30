using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public static class FuncRetryTracker
    {
        public class TrackingContext : IDisposable
        {
            public Guid Id { get; } = Guid.NewGuid();

            private readonly IDictionary<string, int> _functionCallTracker = new Dictionary<string, int>();

            public void TrackCall(string functionName)
            {
                if (_functionCallTracker.TryGetValue(functionName, out var count))
                {
                    _functionCallTracker[functionName] = count + 1;
                }
                else
                {
                    _functionCallTracker.Add(functionName, 1);
                }
            }

            public int GetCallCount(string functionName)
            {
                return _functionCallTracker.TryGetValue(functionName, out var count)
                    ? count
                    : 0;
            }

            public void Dispose()
            {
                TrackingContexts.Remove(Id);
            }
        }

        private static readonly IDictionary<Guid, TrackingContext> TrackingContexts = new Dictionary<Guid, TrackingContext>();

        public static TrackingContext Track()
        {
            var ctx = new TrackingContext();
            TrackingContexts.Add(ctx.Id, ctx);
            return ctx;
        }

        public static TrackingContext GetTracker(Guid id)
        {
            return TrackingContexts[id];
        }
    }

    public partial class Funcs
    {
        [FunctionName(nameof(DurableFunctionWithRetrySucceedingActivity))]
        public static async Task DurableFunctionWithRetrySucceedingActivity(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            using (var tracker = FuncRetryTracker.Track())
            {
                await context.CallActivityWithRetryAsync(
                    nameof(FailAtGivenCallNoActivity),
                    new RetryOptions(TimeSpan.FromMilliseconds(1), 2) { },
                    new RetryingActivitySetup
                    {
                        TrackerId = tracker.Id,
                        SucceedAtCallNo = 2
                    }
                );
            }
        }

        [FunctionName(nameof(DurableFunctionWithRetryFailingActivity))]
        public static async Task DurableFunctionWithRetryFailingActivity(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
        {
            using (var tracker = FuncRetryTracker.Track()) {
                await context.CallActivityWithRetryAsync(
                nameof(FailAtGivenCallNoActivity),
                new RetryOptions(TimeSpan.FromMilliseconds(1), 2) { },
                new RetryingActivitySetup
                {
                    TrackerId = tracker.Id,
                    SucceedAtCallNo = -1
                });
            }
        }

        [FunctionName(nameof(FailAtGivenCallNoActivity))]
        public static Task FailAtGivenCallNoActivity([ActivityTrigger] DurableActivityContextBase context)
        {
            var activitySetup = context.GetInput<RetryingActivitySetup>();
            var tracker = FuncRetryTracker.GetTracker(activitySetup.TrackerId);
            tracker.TrackCall(nameof(FailAtGivenCallNoActivity));

            var currentCallCount = tracker.GetCallCount(nameof(FailAtGivenCallNoActivity));
            if (currentCallCount == activitySetup.SucceedAtCallNo)
            {
                return Task.CompletedTask;
            }

            throw new Exception("Failing call no " + currentCallCount);
        }
    }

    public class RetryingActivitySetup
    {
        public Guid TrackerId { get; set; }
        public int SucceedAtCallNo { get; set; }
    }
}
