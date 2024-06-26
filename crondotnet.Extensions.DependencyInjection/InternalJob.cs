﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace crondotnet
{
    internal sealed class InternalJob : ICronJob
    {
        [ActivatorUtilitiesConstructor]
        public InternalJob(
                IConfiguration configuration,
                string expressionKey,
                ExecuteCronJob job)
        {
            ReloadToken = configuration.GetReloadToken();
            Schedule = new CronSchedule(configuration.GetValue<string>(expressionKey));
            ReloadToken.RegisterChangeCallback(state =>
            {
                Schedule = new CronSchedule(configuration.GetValue<string>(expressionKey));
            }, null);
            Job = job;
        }

        private ExecuteCronJob Job { get; }

        private IChangeToken ReloadToken { get; }

        private ICronSchedule Schedule { get; set; }

        private SemaphoreSlim Semaphore { get; } = new(1);

        public Task ExecuteCronJob(CancellationToken cancellationToken)
        {
            return Job(cancellationToken);
        }

        public async Task Execute(DateTime startTime, CancellationToken cancellationToken)
        {
            try
            {
                await Semaphore.WaitAsync(cancellationToken);

                if (!Schedule.IsTime(startTime))
                    return;

                await Job(cancellationToken);
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}