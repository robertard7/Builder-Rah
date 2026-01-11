#nullable enable

namespace RahBuilder.Workflow.Provider;

public interface IProviderTelemetrySink
{
    void RecordEvent(ProviderDiagnosticEvent evt);
    void RecordMetrics(ProviderMetricsSnapshot metrics);
}
