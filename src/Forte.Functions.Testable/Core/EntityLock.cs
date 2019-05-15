using System;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Core
{
    internal class EntityLock : IDisposable
    {
        public EntityLock(EntityId[] entities)
        {   
        }

        public void Dispose()
        {
        }
    }

    internal static class DelayUtil
    {
        public static TimeSpan? ComputeNextDelay(int attempt, DateTime firstAttempt, Exception failure, RetryOptions retryOptions, DateTime currentUtcDateTime, bool useDelaysForRetries)
        {
            // adapted from 
            // https://github.com/Azure/durabletask/blob/f9cc450539b5e37c97c19ae393d5bb1564fda7a8/src/DurableTask.Core/RetryInterceptor.cs

            if (attempt >= retryOptions.MaxNumberOfAttempts) return null;

            if (!retryOptions.Handle(failure)) return null;

            DateTime retryExpiration = (retryOptions.RetryTimeout != TimeSpan.MaxValue)
                ? firstAttempt.Add(retryOptions.RetryTimeout)
                : DateTime.MaxValue;

            if (currentUtcDateTime >= retryExpiration) return null;

            if (useDelaysForRetries) return TimeSpan.Zero;

            double nextDelayInMilliseconds = retryOptions.FirstRetryInterval.TotalMilliseconds *
                                             Math.Pow(retryOptions.BackoffCoefficient, attempt);
            TimeSpan? nextDelay = nextDelayInMilliseconds < retryOptions.MaxRetryInterval.TotalMilliseconds
                ? TimeSpan.FromMilliseconds(nextDelayInMilliseconds)
                : retryOptions.MaxRetryInterval;

            return nextDelay;
        }
    }
}