using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Repro
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("dotnet tool install --global dotnet-counters");
            Console.WriteLine($"dotnet-counters monitor --process-id {Process.GetCurrentProcess().Id} --counters System.Runtime");
            Console.WriteLine();
            Console.WriteLine("Press any key to get started");
            Console.WriteLine();
            Console.ReadLine();

            // concurrency > 1 is the key
            var concurrency = 2;
            var sender = new RemoteChangesClientBase();
            for (int i = 0; i < 2000; i++)
            {
                Console.WriteLine(i);
                var tasks = new List<Task>(concurrency);
                for (var j = 0; j < concurrency; j++)
                {
                    tasks.Add(sender.Send(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
                }

                await Task.WhenAll(tasks);
            }

            Console.ReadLine();
            Console.WriteLine("Press any key to exit");
        }
    }

    class Request : IDisposable
    {
        private readonly string sendUrl;

        public Request(string sendUrl)
        {
            this.sendUrl = sendUrl;
        }

        public async Task ExecuteRequestAsync() => await Task.Delay(20); // the actual delay is not that important but it needs to sufficiently yield

        public void Dispose()
        {
        }
    }
}
