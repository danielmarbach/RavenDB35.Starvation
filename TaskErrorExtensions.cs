using System;
using System.Threading.Tasks;

namespace Repro
{
    internal static class TaskErrorExtensions
    {
        public static Task ObserveException(this Task self)
        {
            // this merely observe the exception task, nothing else
            self.ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    GC.KeepAlive(task.Exception);
                }
            });
            return self;
        }
    }
}
