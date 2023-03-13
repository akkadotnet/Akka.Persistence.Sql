using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Utility
{
    /// <summary>
    /// An awaiter that never throws.
    /// </summary>
    internal struct NoThrowAwaiter : ICriticalNotifyCompletion
    {
        private readonly Task _task;
        public NoThrowAwaiter(Task task) { _task = task; }
        public NoThrowAwaiter GetAwaiter() => this;
        public bool IsCompleted => _task.IsCompleted;
        public void GetResult() { }
        public void OnCompleted(Action continuation) => _task.GetAwaiter().OnCompleted(continuation);
        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
    }
}
