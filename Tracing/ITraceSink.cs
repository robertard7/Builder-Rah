// Tracing/ITraceSink.cs
#nullable enable
namespace RahOllamaOnly.Tracing
{
    public interface ITraceSink
    {
        void Trace(string line);
    }
}
