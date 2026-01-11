#nullable enable
using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RahBuilder.Settings;
using RahBuilder.Workflow.Provider;

namespace RahBuilder.Settings.Pages;

public sealed class ProviderDiagnosticsPage : UserControl, ISettingsPageProvider
{
    public new string Name => "Provider Health";

    private readonly Label _summary;
    private readonly Label _status;
    private readonly TextBox _events;
    private readonly Panel _detailsPanel;
    private readonly Button _toggleDetails;
    private readonly Button _resetMetrics;

    public ProviderDiagnosticsPage()
    {
        Dock = DockStyle.Fill;

        _summary = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Provider health summary loading…"
        };

        _status = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Status: -"
        };

        _toggleDetails = new Button
        {
            Text = "Show Details",
            Dock = DockStyle.Top,
            AutoSize = true
        };
        _toggleDetails.Click += (_, _) => ToggleDetails();

        _resetMetrics = new Button
        {
            Text = "Reset Provider Metrics",
            Dock = DockStyle.Top,
            AutoSize = true
        };
        _resetMetrics.Click += (_, _) => ProviderDiagnosticsHub.RequestMetricsReset();

        _events = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        };

        _detailsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _detailsPanel.Controls.Add(_events);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(_summary, 0, 0);
        root.Controls.Add(_status, 0, 1);
        root.Controls.Add(_toggleDetails, 0, 2);
        root.Controls.Add(_resetMetrics, 0, 3);
        root.Controls.Add(_detailsPanel, 0, 4);

        Controls.Add(root);

        UpdateMetrics(ProviderDiagnosticsHub.LatestMetrics);
        UpdateEvents(ProviderDiagnosticsHub.Events);
        ProviderDiagnosticsHub.MetricsUpdated += UpdateMetrics;
        ProviderDiagnosticsHub.EventsUpdated += UpdateEvents;
    }

    private void ToggleDetails()
    {
        _detailsPanel.Visible = !_detailsPanel.Visible;
        _toggleDetails.Text = _detailsPanel.Visible ? "Hide Details" : "Show Details";
    }

    private void UpdateMetrics(ProviderMetricsSnapshot metrics)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateMetrics(metrics)));
            return;
        }

        var status = metrics.Enabled
            ? (metrics.Reachable ? "Online" : "Offline")
            : "Disabled";
        if (metrics.IsStale)
            status += " (Stale)";

        _status.Text = $"Status: {status}";

        var uptime = TimeSpan.FromSeconds(metrics.TotalUptimeSeconds);
        var downtime = TimeSpan.FromSeconds(metrics.TotalDowntimeSeconds);
        var lastEvent = metrics.LastEvent?.ToLocalTime().ToString("g") ?? "(none)";
        var lastSuccess = metrics.LastSuccess?.ToLocalTime().ToString("g") ?? "(none)";
        var staleFor = metrics.StaleForSeconds.HasValue ? TimeSpan.FromSeconds(metrics.StaleForSeconds.Value).ToString("g") : "-";

        _summary.Text =
            $"Uptime: {uptime:g} | Downtime: {downtime:g} | Retries: {metrics.RetryAttempts} (ok {metrics.RetrySuccesses}, fail {metrics.RetryFailures})" +
            Environment.NewLine +
            $"Transitions: enable {metrics.EnableTransitions}, disable {metrics.DisableTransitions}" +
            Environment.NewLine +
            $"Last event: {lastEvent} | Last success: {lastSuccess} | Stale for: {staleFor}";
    }

    private void UpdateEvents(IReadOnlyList<ProviderDiagnosticEvent> events)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateEvents(events)));
            return;
        }

        if (events == null || events.Count == 0)
        {
            _events.Text = "No provider events yet.";
            return;
        }

        var sb = new StringBuilder();
        foreach (var evt in events.OrderByDescending(e => e.Timestamp))
        {
            var metadata = evt.Metadata != null && evt.Metadata.Count > 0
                ? " (" + string.Join(", ", evt.Metadata.Select(kv => $"{kv.Key}={kv.Value}")) + ")"
                : "";
            sb.AppendLine($"{evt.Timestamp:HH:mm:ss} [{evt.EventType}] {evt.Message}{metadata}");
        }
        _events.Text = sb.ToString();
    }

    public Control BuildPage(AppConfig config) => new ProviderDiagnosticsPage();
}
