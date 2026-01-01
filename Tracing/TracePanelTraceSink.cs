// Tracing/TracePanelTraceSink.cs
#nullable enable
using RahOllamaOnly.Ui;

namespace RahOllamaOnly.Tracing
{
    public sealed class TracePanelTraceSink : ITraceSink
    {
        private readonly TracePanelWriter _w;

        public TracePanelTraceSink(TracePanelWriter writer) => _w = writer;

        public void Trace(string line)
        {
            // line already contains [TRACE T##]
            _w.TraceLine(line);
        }
    }
}
