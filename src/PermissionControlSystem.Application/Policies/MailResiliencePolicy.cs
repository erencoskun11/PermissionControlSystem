using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;

namespace PermissionControlSystem.Policies
{
    public static class MailResiliencePolicy
    {
        private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not BrokenCircuitException)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();

        public static ResiliencePipeline GetPipeline()
        {
            return Pipeline;
        }
    }
}
