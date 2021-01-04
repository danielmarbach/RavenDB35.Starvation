using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Repro
{
    public class RemoteChangesClientBaseFixed
    {
        private readonly string url = "url";
        private readonly string id = "id";
        private readonly Task worker;
        private ConcurrentQueue<Work> workQueue;
        private CancellationTokenSource tokenSource;
        private CancellationTokenRegistration tokenRegistration;
        volatile TaskCompletionSource<bool> syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public RemoteChangesClientBaseFixed()
        {
            workQueue = new ConcurrentQueue<Work>();
            tokenSource = new CancellationTokenSource();
            tokenRegistration = tokenSource.Token.Register(() => syncSource.TrySetCanceled());
            worker = Worker();
        }

        async Task Worker()
        {
            while (tokenSource.IsCancellationRequested == false)
            {
                try
                {
                    await syncSource.Task.ConfigureAwait(false);
                    syncSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    await WorkUntilEmpty().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // Swallowing the task cancellation in case we are stopping work.
                }
            }

            // drain
            await WorkUntilEmpty().ConfigureAwait(false);
        }

        private async Task WorkUntilEmpty()
        {
            Work work;
            while (workQueue.TryDequeue(out work))
            {
                using (work)
                {
                    try
                    {
                        await work.SendTask.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // intentionally swallowed to keep the previous behavior of ObserveException
                    }
                    finally
                    {
                        work.DoneTask.TrySetResult(true);
                    }
                }
            }
        }

        sealed class Work : IDisposable
        {
            readonly Request request;
            public readonly TaskCompletionSource<bool> DoneTask;
            public readonly Task SendTask;

            public Work(TaskCompletionSource<bool> doneTask, Request request, Task sendTask)
            {
                DoneTask = doneTask;
                this.request = request;
                SendTask = sendTask;
            }

            public void Dispose()
            {
                request.Dispose();
            }
        }

        public Task Send(string command, string value)
        {
            try
            {
                var sendUrlBuilder = new StringBuilder();
                sendUrlBuilder.Append(url);
                sendUrlBuilder.Append("/changes/config?id=");
                sendUrlBuilder.Append(id);
                sendUrlBuilder.Append("&command=");
                sendUrlBuilder.Append(command);

                if (string.IsNullOrEmpty(value) == false)
                {
                    sendUrlBuilder.Append("&value=");
                    sendUrlBuilder.Append(Uri.EscapeUriString(value));
                }

                var request = new Request(sendUrlBuilder.ToString());
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var work = new Work(done, request, request.ExecuteRequestAsync());
                workQueue.Enqueue(work);
                syncSource.TrySetResult(true);
                return done.Task;
            }
            catch (Exception)
            {
                return new CompletedTask();
            }
        }
    }
}
