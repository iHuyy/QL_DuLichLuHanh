using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DuLich.BackgroundServices
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

        public BackgroundTaskQueue(IConfiguration configuration)
        {
            // BoundedChannel ensures the queue doesn't grow indefinitely.
            // If the queue is full, the producer will wait until there is space.
            var capacity = configuration.GetValue<int>("BackgroundTaskQueue:Capacity", 100);

            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
        }

        public async ValueTask QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            // Write the work item to the channel. This is thread-safe.
            await _queue.Writer.WriteAsync(workItem);
        }

        public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            // Read a work item from the channel. This is thread-safe.
            // It will wait until a work item is available.
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);
            return workItem;
        }
    }
}
