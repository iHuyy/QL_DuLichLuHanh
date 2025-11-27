using System;
using System.Threading;
using System.Threading.Tasks;

namespace DuLich.BackgroundServices
{
    public interface IBackgroundTaskQueue
    {
        // Adds a work item to the queue.
        // A work item is a function that accepts a scoped IServiceProvider and a CancellationToken.
        ValueTask QueueBackgroundWorkItemAsync(Func<IServiceProvider, CancellationToken, Task> workItem);

        // Dequeues a work item. This will be called by the hosted service.
        ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}
