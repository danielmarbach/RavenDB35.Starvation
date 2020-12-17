using System;
using System.Threading.Tasks;

namespace Repro
{
    public class RemoteChangesClientBase
    {
        private Task lastSendTask;
        private readonly string url = "url";
        private readonly string id = "id";

        public Task Send(string command, string value)
        {
            lock (this)
            {
                var sendTask = lastSendTask;
                if (sendTask != null)
                {
                    return sendTask.ContinueWith(_ =>
                    {
                        Send(command, value);
                    });
                }

                try
                {
                    var sendUrl = url + "/changes/config?id=" + id + "&command=" + command;
                    if (string.IsNullOrEmpty(value) == false)
                        sendUrl += "&value=" + Uri.EscapeUriString(value);

                    var request = new Request(sendUrl);
                    lastSendTask = request.ExecuteRequestAsync().ObserveException();

                    return lastSendTask.ContinueWith(task =>
                    {
                        lastSendTask = null;
                        request.Dispose();
                    });
                }
                catch (Exception e)
                {
                    return new CompletedTask(e).Task.ObserveException();
                }
            }
        }
    }
}
