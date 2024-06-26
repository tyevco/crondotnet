﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace crondotnet
{
    internal sealed class InternalJob<TService> : ICronJob
            where TService : IThinService
    {
        [ActivatorUtilitiesConstructor]
        public InternalJob(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            string expressionKey)
        {
            ReloadToken = configuration.GetReloadToken();
            Schedule = new CronSchedule(configuration.GetValue<string>(expressionKey));
            ReloadToken.RegisterChangeCallback(state =>
            {
                Schedule = new CronSchedule(configuration.GetValue<string>(expressionKey));
            }, null);
            ScopeFactory = scopeFactory;
        }

        private IServiceScopeFactory ScopeFactory { get; }

        private IChangeToken ReloadToken { get; }

        private ICronSchedule Schedule { get; set; }

        private SemaphoreSlim Semaphore { get; } = new(1);

        public async Task Execute(DateTime startTime, CancellationToken cancellationToken)
        {
            try
            {
                await Semaphore.WaitAsync(cancellationToken);

                if (!Schedule.IsTime(startTime))
                    return;

                using var scope = ScopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                if (service != null)
                {
                    await service.Run(cancellationToken);
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}