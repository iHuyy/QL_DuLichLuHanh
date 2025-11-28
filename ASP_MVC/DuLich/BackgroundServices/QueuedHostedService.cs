using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuLich.BackgroundServices
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBackgroundTaskQueue _taskQueue;

        public QueuedHostedService(
            ILogger<QueuedHostedService> logger,
            IServiceProvider serviceProvider,
            IBackgroundTaskQueue taskQueue)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _taskQueue = taskQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.");
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dequeue a work item. This will wait until a work item is available.
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    // Create a new DI scope for the task.
                    // This is crucial for ensuring that scoped services (like DbContext)
                    // are handled correctly.
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // Execute the work item, passing it the scoped service provider.
                        await workItem(scope.ServiceProvider, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");
            await base.StopAsync(stoppingToken);
        }
    }
}
