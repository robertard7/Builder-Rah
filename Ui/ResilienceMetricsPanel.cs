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
    private readonly CheckedListBox _toolLegend;
    private readonly ListView _alertList;
    private readonly Label _alertLabel;
    private readonly NumericUpDown _openThreshold;
    private readonly NumericUpDown _retryThreshold;
    private readonly CheckBox _alertsEnabled;
    private readonly CheckBox _showAcknowledgedAlerts;
    private readonly Button _copySnapshot;
    private readonly TrackBar _historyStart;
    private readonly TrackBar _historyEnd;
    private readonly Label _historyLabel;
    private readonly Timer _timer;
    private readonly Dictionary<string, List<ResilienceMetricsSample>> _toolHistory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _toolSeriesVisible = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ResilienceMetricsSample> _historyCache = new List<ResilienceMetricsSample>();
    private DateTimeOffset _lastHistoryRefresh = DateTimeOffset.MinValue;
    private (int startMinutes, int endMinutes) _historyRangeMinutes = (60, 0);
    private bool _historyDirty = true;
    private bool _alertActive;

    public event Action<string>? AlertTriggered;

    public ResilienceMetricsPanel()
    {
        Dock = DockStyle.Fill;

        _chart = BuildChart();
        _toolList = BuildToolList();
        _toolLegend = BuildToolLegend();
        _alertList = BuildAlertList();
        _alertLabel = new Label { AutoSize = true, ForeColor = Color.SeaGreen, Text = "Alerts: OK" };
        _openThreshold = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 5, Width = 80 };
        _retryThreshold = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 25, Width = 80 };
        _alertsEnabled = new CheckBox { Text = "Enable alerts", Checked = true, AutoSize = true };
        _showAcknowledgedAlerts = new CheckBox { Text = "Show acknowledged", Checked = false, AutoSize = true };
        _showAcknowledgedAlerts.CheckedChanged += (_, _) => UpdateAlertList();
        _copySnapshot = new Button { Text = "Copy snapshot", AutoSize = true };
        _copySnapshot.Click += (_, _) => CopySnapshot();
        _historyStart = new TrackBar { Minimum = 10, Maximum = 240, TickFrequency = 30, Value = 60, Width = 180, AutoSize = false, Height = 32 };
        _historyEnd = new TrackBar { Minimum = 0, Maximum = 120, TickFrequency = 15, Value = 0, Width = 180, AutoSize = false, Height = 32 };
        _historyLabel = new Label { AutoSize = true, Text = "History: last 60m" };
        _historyStart.Scroll += (_, _) => UpdateHistoryRange();
        _historyEnd.Scroll += (_, _) => UpdateHistoryRange();

        var thresholdPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 6, 6, 6)
        };
        thresholdPanel.Controls.Add(_alertsEnabled);
        thresholdPanel.Controls.Add(_showAcknowledgedAlerts);
        thresholdPanel.Controls.Add(new Label { Text = "Open/hr >", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_openThreshold);
        thresholdPanel.Controls.Add(new Label { Text = "Retry/hr >", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_retryThreshold);
        thresholdPanel.Controls.Add(_alertLabel);
        thresholdPanel.Controls.Add(_copySnapshot);
        thresholdPanel.Controls.Add(new Label { Text = "Start (m ago)", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_historyStart);
        thresholdPanel.Controls.Add(new Label { Text = "End (m ago)", AutoSize = true, Padding = new Padding(6, 6, 0, 0) });
        thresholdPanel.Controls.Add(_historyEnd);
        thresholdPanel.Controls.Add(_historyLabel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 520
        };
        split.Panel1.Controls.Add(_chart);
        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 240
        };
        var toolPanel = new Panel { Dock = DockStyle.Fill };
        toolPanel.Controls.Add(_toolList);
        toolPanel.Controls.Add(_toolLegend);
        rightSplit.Panel1.Controls.Add(toolPanel);
        rightSplit.Panel2.Controls.Add(_alertList);
        split.Panel2.Controls.Add(rightSplit);

        Controls.Add(split);
        Controls.Add(thresholdPanel);

        _timer = new Timer { Interval = 2000 };
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
        UpdateHistoryRange();
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
        chart.Series.Add(BuildAlertSeries());

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

    private static Series BuildAlertSeries()
    {
        return new Series("Alerts")
        {
            ChartType = SeriesChartType.Point,
            XValueType = ChartValueType.DateTime,
            Color = Color.Firebrick,
            MarkerStyle = MarkerStyle.Triangle,
            MarkerSize = 8,
            IsVisibleInLegend = false
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

    private CheckedListBox BuildToolLegend()
    {
        var list = new CheckedListBox
        {
            Dock = DockStyle.Right,
            Width = 180
        };
        list.ItemCheck += (_, _) => _timer.BeginInvoke((MethodInvoker)UpdateToolSeriesVisibility);
        return list;
    }

    private static ListView BuildAlertList()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        list.Columns.Add("Triggered", 140);
        list.Columns.Add("Severity", 80);
        list.Columns.Add("Alert", 240);
        list.Columns.Add("Status", 100);
        list.Columns.Add("Action", 80);
        list.MouseClick += (_, args) => HandleAlertClick(list, args);
        return list;
    }

    private void RefreshMetrics()
    {
        var metrics = ResilienceDiagnosticsHub.Snapshot();
        var byTool = ResilienceDiagnosticsHub.SnapshotByTool();
        UpdateToolHistory(byTool);
        var history = LoadHistory();
        var alertHistory = ResilienceDiagnosticsHub.SnapshotHistory(TimeSpan.FromHours(1), 120);
        var alertEvents = ResilienceDiagnosticsHub.ListAlertEvents(50, includeAcknowledged: _showAcknowledgedAlerts.Checked);
        UpdateChart(history, alertEvents);
        UpdateToolList(byTool);
        UpdateAlerts(metrics, alertHistory);
        UpdateAlertList();
    }

    private void UpdateChart(IReadOnlyList<ResilienceMetricsSample> history, IReadOnlyList<ResilienceAlertEvent> alerts)
    {
        var openSeries = _chart.Series["Open"];
        var halfOpenSeries = _chart.Series["HalfOpen"];
        var closedSeries = _chart.Series["Closed"];
        var retrySeries = _chart.Series["RetryAttempts"];
        var alertSeries = _chart.Series["Alerts"];

        openSeries.Points.Clear();
        halfOpenSeries.Points.Clear();
        closedSeries.Points.Clear();
        retrySeries.Points.Clear();
        alertSeries.Points.Clear();

        foreach (var sample in history)
        {
            var x = sample.Timestamp.UtcDateTime.ToOADate();
            openSeries.Points.AddXY(x, sample.Metrics.OpenCount);
            halfOpenSeries.Points.AddXY(x, sample.Metrics.HalfOpenCount);
            closedSeries.Points.AddXY(x, sample.Metrics.ClosedCount);
            retrySeries.Points.AddXY(x, sample.Metrics.RetryAttempts);
        }

        foreach (var alert in alerts)
        {
            var alertPoint = new DataPoint(alert.TriggeredAt.UtcDateTime.ToOADate(), openSeries.Points.LastOrDefault()?.YValues.FirstOrDefault() ?? 0)
            {
                MarkerColor = alert.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase) ? Color.DarkRed : Color.Goldenrod
            };
            alertSeries.Points.Add(alertPoint);
        }

        UpdateToolSeries();
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

        UpdateToolLegend(byTool.Keys);
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

    private void UpdateAlertList()
    {
        var eventsList = ResilienceDiagnosticsHub.ListAlertEvents(50, includeAcknowledged: _showAcknowledgedAlerts.Checked)
            .Where(e => _showAcknowledgedAlerts.Checked || !e.Acknowledged)
            .Take(20)
            .ToList();
        _alertList.BeginUpdate();
        _alertList.Items.Clear();
        foreach (var alert in eventsList)
        {
            var item = new ListViewItem(alert.TriggeredAt.ToLocalTime().ToString("g"));
            item.SubItems.Add(alert.Severity);
            item.SubItems.Add(alert.Message);
            item.SubItems.Add(alert.Acknowledged ? "Acknowledged" : "Active");
            item.SubItems.Add(alert.Acknowledged ? "" : "Resolve");
            if (alert.Acknowledged)
                item.ForeColor = Color.DimGray;
            else if (string.Equals(alert.Severity, "critical", StringComparison.OrdinalIgnoreCase))
                item.ForeColor = Color.Firebrick;
            item.Tag = alert.Id;
            _alertList.Items.Add(item);
        }
        _alertList.EndUpdate();
    }

    private void CopySnapshot()
    {
        var metrics = ResilienceDiagnosticsHub.Snapshot();
        var byTool = ResilienceDiagnosticsHub.SnapshotByTool();
        var snapshot = ResilienceMetricsSnapshotFormatter.BuildSnapshot(metrics, byTool, maxTools: 10);
        Clipboard.SetText(snapshot);
    }

    private void UpdateHistoryRange()
    {
        var startMinutes = Math.Max(_historyStart.Value, _historyEnd.Value + 1);
        _historyStart.Value = startMinutes;
        _historyRangeMinutes = (startMinutes, _historyEnd.Value);
        _historyLabel.Text = $"History: {startMinutes}m → {_historyEnd.Value}m ago";
        _historyDirty = true;
    }

    private IReadOnlyList<ResilienceMetricsSample> LoadHistory()
    {
        var now = DateTimeOffset.UtcNow;
        if (_historyDirty || now - _lastHistoryRefresh > TimeSpan.FromSeconds(5))
        {
            var start = now - TimeSpan.FromMinutes(_historyRangeMinutes.startMinutes);
            var end = now - TimeSpan.FromMinutes(_historyRangeMinutes.endMinutes);
            _historyCache = ResilienceDiagnosticsHub.SnapshotHistoryRange(start, end, limit: 300, bucketMinutes: 0);
            _lastHistoryRefresh = now;
            _historyDirty = false;
        }
        return _historyCache;
    }

    private void UpdateToolHistory(IReadOnlyDictionary<string, CircuitMetricsSnapshot> byTool)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in byTool)
        {
            if (!_toolHistory.TryGetValue(entry.Key, out var list))
            {
                list = new List<ResilienceMetricsSample>();
                _toolHistory[entry.Key] = list;
            }
            list.Add(new ResilienceMetricsSample(now, entry.Value));
        }

        var cutoff = now - TimeSpan.FromHours(2);
        foreach (var list in _toolHistory.Values)
            list.RemoveAll(sample => sample.Timestamp < cutoff);
    }

    private void UpdateToolLegend(IEnumerable<string> toolNames)
    {
        var existing = _toolLegend.Items.Cast<string>().ToList();
        foreach (var name in existing)
        {
            if (!toolNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                _toolLegend.Items.Remove(name);
        }

        foreach (var name in toolNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!_toolLegend.Items.Contains(name))
            {
                _toolLegend.Items.Add(name, _toolSeriesVisible.TryGetValue(name, out var visible) && visible);
            }
        }
    }

    private void UpdateToolSeriesVisibility()
    {
        for (var i = 0; i < _toolLegend.Items.Count; i++)
        {
            var name = _toolLegend.Items[i]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                continue;
            _toolSeriesVisible[name] = _toolLegend.GetItemChecked(i);
        }
        UpdateToolSeries();
    }

    private void UpdateToolSeries()
    {
        var activeTools = _toolSeriesVisible.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seriesToRemove = _chart.Series.Where(s => s.Name.StartsWith("Tool:", StringComparison.OrdinalIgnoreCase) && !activeTools.Contains(s.Name["Tool:".Length..])).ToList();
        foreach (var series in seriesToRemove)
            _chart.Series.Remove(series);

        foreach (var tool in activeTools)
        {
            var seriesName = $"Tool:{tool}";
            if (!_chart.Series.IsUniqueName(seriesName))
            {
                var series = _chart.Series[seriesName];
                series.Points.Clear();
                AddToolSeriesPoints(series, tool);
                continue;
            }

            var toolSeries = new Series(seriesName)
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime,
                BorderDashStyle = ChartDashStyle.Dash,
                BorderWidth = 1,
                Color = Color.SteelBlue,
                LegendText = tool
            };
            AddToolSeriesPoints(toolSeries, tool);
            _chart.Series.Add(toolSeries);
        }
    }

    private void AddToolSeriesPoints(Series series, string tool)
    {
        if (!_toolHistory.TryGetValue(tool, out var samples))
            return;
        var now = DateTimeOffset.UtcNow;
        var start = now - TimeSpan.FromMinutes(_historyRangeMinutes.startMinutes);
        var end = now - TimeSpan.FromMinutes(_historyRangeMinutes.endMinutes);
        foreach (var sample in samples.Where(s => s.Timestamp >= start && s.Timestamp <= end))
        {
            series.Points.AddXY(sample.Timestamp.UtcDateTime.ToOADate(), sample.Metrics.OpenCount);
        }
    }

    private static void HandleAlertClick(ListView list, MouseEventArgs args)
    {
        var hit = list.HitTest(args.Location);
        if (hit.Item == null || hit.SubItem == null)
            return;
        var subIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
        if (subIndex != 4)
            return;
        var eventId = hit.Item.Tag as string;
        if (string.IsNullOrWhiteSpace(eventId))
            return;
        ResilienceDiagnosticsHub.AcknowledgeAlertEvent(eventId);
    }

}
