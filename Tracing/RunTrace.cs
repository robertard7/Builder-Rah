// Tracing/RunTrace.cs
#nullable enable
using System.Threading;

namespace RahOllamaOnly.Tracing
{
    public sealed class RunTrace
    {
        private int _step;
        private readonly ITraceSink _sink;

        public RunTrace(ITraceSink sink) => _sink = sink;

        public void Emit(string message)
        {
            var n = Interlocked.Increment(ref _step);
            _sink.Trace($"[TRACE T{n:00}] {message}");
        }
    }
}
