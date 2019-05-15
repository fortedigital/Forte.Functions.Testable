using System;
using System.Threading;
using System.Threading.Tasks;

namespace Forte.Functions.Testable.Core
{
    internal class OrchestrationTimer : CancellationTokenSource
    {
        private DateTime StartedAt { get; }
        private DateTime FireAt { get; }
        public OrchestrationTimer(DateTime fireAt, DateTime startedAt, CancellationToken cancelToken)
        {
            StartedAt = startedAt;
            FireAt = fireAt;

            cancelToken.Register(Cancel);
        }

        public async Task Wait()
        {
            try
            {
                var delay = FireAt - StartedAt;
                await Task.Delay(delay, this.Token);
            }
            catch (TaskCanceledException)
            {
                if (!_swallowTaskCancelledException) throw;
            }
        }

        private bool _swallowTaskCancelledException = false;

        public void TimeChanged(DateTime newTime)
        {
            if (newTime < FireAt) return;

            _swallowTaskCancelledException = true;
            Cancel();
        }
    }
}