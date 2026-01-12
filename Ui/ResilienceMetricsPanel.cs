#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using RahOllamaOnly.Metrics;

namespace RahBuilder.Ui;

public sealed class ResilienceMetricsPanel : UserControl
{
    private readonly Chart _chart;
    private readonly ListView _toolList;
    private readonly Label _alertLabel;
    private readonly NumericUpDown _openThreshold;
    private readonly NumericUpDown _retryThreshold;
    private readonly CheckBox _alertsEnabled;
    private readonly Button _copySnapshot;
    private readonly Timer _timer;
    private bool _alertActive;

    public event Action<string>? AlertTriggered;

    public ResilienceMetricsPanel()
    {
        Dock = DockStyle.Fill;

        _chart = BuildChart();
        _toolList = BuildToolList();
        _alertLabel = new Label { AutoSize = true, ForeColor = Color.SeaGreen, Text = "Alerts: OK" };
        _openThreshold = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 5, Width = 80 };
        _retryThreshold = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 25, Width = 80 };
        _alertsEnabled = new CheckBox { Text = "Enable alerts", Checked = true, AutoSize = true };
        _copySnapshot = new Button { Text = "Copy snapshot", AutoSize = true };
        _copySnapshot.Click += (_, _) => CopySnapshot();

        var thresholdPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 6, 6, 6)
        };
        thresholdPanel.Controls.Add(_alertsEnabled);
        thresholdPanel.Controls.Add(new Label { Text = "Open/hr >", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_openThreshold);
        thresholdPanel.Controls.Add(new Label { Text = "Retry/hr >", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_retryThreshold);
        thresholdPanel.Controls.Add(_alertLabel);
        thresholdPanel.Controls.Add(_copySnapshot);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520
        };
        split.Panel1.Controls.Add(_chart);
        split.Panel2.Controls.Add(_toolList);

        Controls.Add(split);
        Controls.Add(thresholdPanel);

        _timer = new Timer { Interval = 2000 };
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
        RefreshMetrics();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Chart BuildChart()
    {
        var chart = new Chart { Dock = DockStyle.Fill };
        var area = new ChartArea("Resilience");
        area.AxisX.LabelStyle.Format = "HH:mm:ss";
        area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
        area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
        chart.ChartAreas.Add(area);

        chart.Legends.Add(new Legend { Docking = Docking.Top });

        chart.Series.Add(BuildSeries("Open", Color.Firebrick));
        chart.Series.Add(BuildSeries("HalfOpen", Color.DarkOrange));
        chart.Series.Add(BuildSeries("Closed", Color.SeaGreen));
        chart.Series.Add(BuildSeries("RetryAttempts", Color.SlateBlue));

        return chart;
    }

    private static Series BuildSeries(string name, Color color)
    {
        return new Series(name)
        {
            ChartType = SeriesChartType.Line,
            XValueType = ChartValueType.DateTime,
            Color = color,
            BorderWidth = 2
        };
    }

    private static ListView BuildToolList()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        list.Columns.Add("Tool", 220);
        list.Columns.Add("Open", 70);
        list.Columns.Add("Retry", 70);
        return list;
    }

    private void RefreshMetrics()
    {
        var metrics = ResilienceDiagnosticsHub.Snapshot();
        var byTool = ResilienceDiagnosticsHub.SnapshotByTool();
        var history = ResilienceDiagnosticsHub.SnapshotHistory(TimeSpan.FromMinutes(30), 120);
        var alertHistory = ResilienceDiagnosticsHub.SnapshotHistory(TimeSpan.FromHours(1), 120);
        UpdateChart(history);
        UpdateToolList(byTool);
        UpdateAlerts(metrics, alertHistory);
    }

    private void UpdateChart(IReadOnlyList<ResilienceMetricsSample> history)
    {
        var openSeries = _chart.Series["Open"];
        var halfOpenSeries = _chart.Series["HalfOpen"];
        var closedSeries = _chart.Series["Closed"];
        var retrySeries = _chart.Series["RetryAttempts"];

        openSeries.Points.Clear();
        halfOpenSeries.Points.Clear();
        closedSeries.Points.Clear();
        retrySeries.Points.Clear();

        foreach (var sample in history)
        {
            var x = sample.Timestamp.UtcDateTime.ToOADate();
            openSeries.Points.AddXY(x, sample.Metrics.OpenCount);
            halfOpenSeries.Points.AddXY(x, sample.Metrics.HalfOpenCount);
            closedSeries.Points.AddXY(x, sample.Metrics.ClosedCount);
            retrySeries.Points.AddXY(x, sample.Metrics.RetryAttempts);
        }

        _chart.ChartAreas[0].RecalculateAxesScale();
    }

    private void UpdateToolList(IReadOnlyDictionary<string, CircuitMetricsSnapshot> byTool)
    {
        _toolList.BeginUpdate();
        _toolList.Items.Clear();

        foreach (var entry in byTool.OrderByDescending(kvp => kvp.Value.OpenCount)
                     .ThenByDescending(kvp => kvp.Value.RetryAttempts)
                     .Take(20))
        {
            var item = new ListViewItem(entry.Key);
            item.SubItems.Add(entry.Value.OpenCount.ToString());
            item.SubItems.Add(entry.Value.RetryAttempts.ToString());
            _toolList.Items.Add(item);
        }

        _toolList.EndUpdate();
    }

    private void UpdateAlerts(CircuitMetricsSnapshot metrics, IReadOnlyList<ResilienceMetricsSample> history)
    {
        if (!_alertsEnabled.Checked)
        {
            _alertLabel.Text = "Alerts: disabled";
            _alertLabel.ForeColor = Color.DimGray;
            _alertActive = false;
            return;
        }

        var baseline = history.FirstOrDefault();
        var openRate = baseline == null ? 0 : metrics.OpenCount - baseline.Metrics.OpenCount;
        var retryRate = baseline == null ? 0 : metrics.RetryAttempts - baseline.Metrics.RetryAttempts;

        var openThreshold = (int)_openThreshold.Value;
        var retryThreshold = (int)_retryThreshold.Value;
        var triggered = openRate > openThreshold || retryRate > retryThreshold;

        if (triggered)
        {
            _alertLabel.Text = $"Alerts: OPEN/hr={openRate}, RETRY/hr={retryRate}";
            _alertLabel.ForeColor = Color.Firebrick;
        }
        else
        {
            _alertLabel.Text = $"Alerts: OPEN/hr={openRate}, RETRY/hr={retryRate}";
            _alertLabel.ForeColor = Color.SeaGreen;
        }

        if (triggered && !_alertActive)
        {
            _alertActive = true;
            AlertTriggered?.Invoke($"Resilience alert triggered (open/hr={openRate}, retry/hr={retryRate}).");
        }
        else if (!triggered)
        {
            _alertActive = false;
        }
    }

    private void CopySnapshot()
    {
        var metrics = ResilienceDiagnosticsHub.Snapshot();
        var byTool = ResilienceDiagnosticsHub.SnapshotByTool();
        var snapshot = ResilienceMetricsSnapshotFormatter.BuildSnapshot(metrics, byTool, maxTools: 10);
        Clipboard.SetText(snapshot);
    }

}
