#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RahBuilder.Settings;
using RahBuilder.Workflow;
using RahBuilder.Workflow.Provider;
using RahBuilder.Ui;
using RahOllamaOnly.Tools.Diagnostics;
using RahOllamaOnly.Tracing;
using RahOllamaOnly.Ui;

namespace RahBuilder;

public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private AttachmentInbox _inbox;

    private readonly RichTextBox _chatView;

    private readonly TextBox _traceBox;
    private readonly TracePanelWriter _traceWriter;
    private readonly RunTrace _trace;

    private readonly WorkflowFacade _workflow;

    private readonly ChatComposerControl _composer;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _modeStatus;
    private readonly ToolStripStatusLabel _repoStatus;
    private readonly ToolStripStatusLabel _attachmentsStatus;
    private readonly ToolStripStatusLabel _workflowStatus;
    private readonly LinkLabel _providerBadge;
    private readonly ToolTip _providerToolTip;
    private readonly LinkLabel _providerSettingsLink;
    private readonly TabPage _settingsTab;
    private readonly SettingsHostControl _settingsControl;
    private bool _lastProviderEnabled;
    private ToolingDiagnostics _toolingDiagnostics = ToolingDiagnostics.Empty;
    private readonly SessionStore _sessionStore;
    private readonly SessionPanel _sessionPanel;
    private readonly List<OutputCard> _cards = new();
    private readonly Button _downloadButton;
    private readonly ListBox _cardList;
    private readonly RichTextBox _cardDetail;
    private readonly TreeView _artifactTree;
    private readonly RichTextBox _artifactPreview;
    private readonly Dictionary<string, string> _filePreviews = new(StringComparer.OrdinalIgnoreCase);
    private readonly TabControl _mainTabs;
    private readonly TabPage _diagnosticsTab;

    public MainForm()
    {
        Text = "RAH Builder (Reset: Settings-First)";
        Width = 1200;
        Height = 800;

        _traceWriter = new TracePanelWriter();
        _trace = new RunTrace(new RahOllamaOnly.Tracing.TracePanelTraceSink(_traceWriter));

        _config = ConfigStore.Load();
        _sessionStore = new SessionStore();
        _inbox = new AttachmentInbox(_config.General, _trace);

        _workflow = new WorkflowFacade(_trace);
        _workflow.UserFacingMessage += OnUserFacingMessage;
        _workflow.StatusChanged += UpdateStatus;
        _workflow.OutputCardProduced += OnOutputCardProduced;
        _workflow.TraceAttentionRequested += ShowTracePane;
        _workflow.PlanReady += OnPlanReady;
        _workflow.ProviderStateChanged += OnProviderStateChanged;
        ProviderDiagnosticsHub.MetricsUpdated += _ => OnProviderMetricsUpdated();

        _mainTabs = new TabControl { Dock = DockStyle.Fill };

        _chatView = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
            BackColor = SystemColors.Window
        };

        _composer = new ChatComposerControl(_inbox);
        _composer.SendRequested += text => _ = SendNowAsync(text);
        _composer.AttachmentsChanged += att => _workflow.SetAttachments(att);
        _composer.RetryProviderRequested += () =>
        {
            _workflow.RetryProvider();
            NotifyProviderRetry();
        };
        _composer.ProviderStatusClicked += OpenProvidersSettings;
        _composer.ToolchainStatusClicked += OpenToolchainSettings;
        _composer.ExecutionStatusClicked += OpenGeneralSettings;
        _workflow.SetAttachments(_inbox.List());
        _providerToolTip = new ToolTip();
        _providerBadge = new LinkLabel
        {
            AutoSize = true,
            LinkBehavior = LinkBehavior.NeverUnderline,
            Text = "Provider: -",
            Margin = new Padding(0, 0, 0, 6)
        };
        _providerBadge.LinkClicked += (_, _) => OpenProvidersSettings();
        _providerToolTip.SetToolTip(_providerBadge, "Local Provider enabled/disabled; click to open settings");
        _providerSettingsLink = new LinkLabel
        {
            AutoSize = true,
            Text = "Settings",
            LinkBehavior = LinkBehavior.NeverUnderline,
            Margin = new Padding(8, 0, 0, 6)
        };
        _providerSettingsLink.LinkClicked += (_, _) => OpenProvidersSettings();

        var providerHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 6, 6, 0)
        };
        providerHeader.Controls.Add(_providerBadge);
        providerHeader.Controls.Add(_providerSettingsLink);

        var chatPanel = new Panel { Dock = DockStyle.Fill };
        chatPanel.Controls.Add(_chatView);
        chatPanel.Controls.Add(_composer);
        chatPanel.Controls.Add(providerHeader);
        _composer.BringToFront();
        providerHeader.BringToFront();
        _chatView.SendToBack();

        // Diagnostics: trace + output cards
        _traceBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 9f)
        };

        _cardList = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };
        _cardList.SelectedIndexChanged += (_, _) => ShowSelectedCard();

        _downloadButton = new Button
        {
            Text = "Download ZIP",
            Dock = DockStyle.Top,
            Height = 30,
            Enabled = false
        };
        _downloadButton.Click += (_, _) => DownloadSelectedArtifact();

        _cardDetail = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            Font = new Font("Segoe UI", 9f)
        };

        _artifactTree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false
        };
        _artifactTree.AfterSelect += (_, _) => ShowArtifactPreview();

        _artifactPreview = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 9f)
        };

        var outputSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 200,
            SplitterDistance = 260
        };
        var cardPanel = new Panel { Dock = DockStyle.Fill };
        cardPanel.Controls.Add(_downloadButton);
        cardPanel.Controls.Add(_cardList);
        outputSplit.Panel1.Controls.Add(cardPanel);
        var artifactSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 150,
            SplitterDistance = 220
        };
        artifactSplit.Panel1.Controls.Add(_artifactTree);
        artifactSplit.Panel2.Controls.Add(_artifactPreview);

        var detailTabs = new TabControl { Dock = DockStyle.Fill };
        detailTabs.TabPages.Add(new TabPage("Details") { Controls = { _cardDetail } });
        detailTabs.TabPages.Add(new TabPage("Artifacts") { Controls = { artifactSplit } });

        outputSplit.Panel2.Controls.Add(detailTabs);

        var auxTabs = new TabControl { Dock = DockStyle.Fill };
        var tracePanel = new Panel { Dock = DockStyle.Fill };
        var reinitProvider = new Button
        {
            Text = "Reinitialize Provider",
            Dock = DockStyle.Top,
            Height = 28
        };
        reinitProvider.Click += (_, _) =>
        {
            _workflow.RetryProvider();
            NotifyProviderRetry();
        };
        tracePanel.Controls.Add(_traceBox);
        tracePanel.Controls.Add(reinitProvider);
        auxTabs.TabPages.Add(new TabPage("Trace") { Controls = { tracePanel } });
        auxTabs.TabPages.Add(new TabPage("Outputs") { Controls = { outputSplit } });
        _sessionPanel = new SessionPanel(_sessionStore);
        _sessionPanel.SessionLoaded += LoadSessionState;
        auxTabs.TabPages.Add(new TabPage("Sessions") { Controls = { _sessionPanel } });

        _traceWriter.Updated += () =>
        {
            if (!IsHandleCreated) return;

            BeginInvoke(new Action(() =>
            {
                _traceBox.Text = _traceWriter.Snapshot();
                _traceBox.SelectionStart = _traceBox.TextLength;
                _traceBox.ScrollToCaret();
            }));
        };

        _mainTabs.TabPages.Add(new TabPage("Chat") { Controls = { chatPanel } });
        _diagnosticsTab = new TabPage("Diagnostics") { Controls = { auxTabs } };
        _mainTabs.TabPages.Add(_diagnosticsTab);

        // Settings tab
        _settingsControl = new SettingsHostControl(_config, AfterSettingsSaved, _trace) { Dock = DockStyle.Fill };
        _settingsTab = new TabPage("Settings") { Controls = { _settingsControl } };
        _mainTabs.TabPages.Add(_settingsTab);

        Controls.Add(_mainTabs);

        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _modeStatus = new ToolStripStatusLabel("Mode: -");
        _repoStatus = new ToolStripStatusLabel("Repo: -");
        _attachmentsStatus = new ToolStripStatusLabel("Attachments: 0/0");
        _workflowStatus = new ToolStripStatusLabel("Workflow: Idle");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _modeStatus, _repoStatus, _attachmentsStatus, _workflowStatus });
        Controls.Add(_statusStrip);

        PublishConfigToRuntime();
        _lastProviderEnabled = _config.General.ProviderEnabled;
        UpdateProviderBadge();
        UpdateStatusLine();
        TryLoadLastSession();

        if (_config.General.EnableGlobalClipboardShortcuts)
            ClipboardPolicy.Apply(this);

        Shown += (_, _) => _composer.FocusInput();
        FormClosed += (_, _) =>
        {
            _workflow.UserFacingMessage -= OnUserFacingMessage;
            _workflow.StatusChanged -= UpdateStatus;
            _workflow.OutputCardProduced -= OnOutputCardProduced;
            _workflow.TraceAttentionRequested -= ShowTracePane;
            _workflow.PlanReady -= OnPlanReady;
            _workflow.ProviderStateChanged -= OnProviderStateChanged;
            ToolingDiagnosticsHub.Updated -= OnToolingDiagnosticsUpdated;
        };

        ToolingDiagnosticsHub.Updated += OnToolingDiagnosticsUpdated;
        UpdateStatus(null);
    }

    private void OnUserFacingMessage(string s)
    {
        if (!IsHandleCreated) return;

        // ALWAYS marshal to UI thread.
        BeginInvoke(new Action(() =>
        {
            AppendChat(s + "\n");
        }));
    }

    private void PublishConfigToRuntime()
    {
        MermaidGraphHub.Publish(_config.WorkflowGraph.MermaidText ?? "");
        _workflow.RefreshTooling(_config);
    }

    private void AfterSettingsSaved()
    {
        PublishConfigToRuntime();
        _inbox = new AttachmentInbox(_config.General, _trace);
        _composer.SetInbox(_inbox);
        _composer.ReloadAttachments(_inbox.List());
        _workflow.SetAttachments(_inbox.List());

        if (_lastProviderEnabled != _config.General.ProviderEnabled)
        {
            _workflow.UpdateProviderEnabled(_config.General.ProviderEnabled);
            if (_config.General.ProviderEnabled)
            {
                _workflow.ResetProviderState();
                _workflow.MarkProviderReachable(true, "enabled");
            }
        }
        _lastProviderEnabled = _config.General.ProviderEnabled;
        UpdateProviderBadge();
        UpdateStatusLine();

        if (_config.General.EnableGlobalClipboardShortcuts)
            ClipboardPolicy.Apply(this);
    }

    private async Task SendNowAsync(string text)
    {
        AppendChat($"> {text}\n");

        _composer.SetEnabled(false);

        try
        {
            await _workflow.RouteUserInput(_config, text, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendChat("[error] " + ex.Message + "\n");
        }
        finally
        {
            _composer.SetEnabled(true);
            _composer.FocusInput();
        }
    }

    private void AppendChat(string s)
    {
        _chatView.AppendText(s);
        _chatView.SelectionStart = _chatView.TextLength;
        _chatView.ScrollToCaret();
    }

    private void UpdateStatus(WorkflowFacade.WorkflowStatusSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            _modeStatus.Text = "Mode: -";
            _repoStatus.Text = "Repo: -";
            _attachmentsStatus.Text = "Attachments: 0/0";
            _workflowStatus.Text = "Workflow: Idle";
            return;
        }

        _modeStatus.Text = $"Mode: {snapshot.Mode}";
        _repoStatus.Text = $"Repo: {snapshot.Repo}";
        _attachmentsStatus.Text = $"Attachments: {snapshot.AttachmentsActive}/{snapshot.AttachmentsTotal}";
        _workflowStatus.Text = $"Workflow: {snapshot.Workflow}";
    }

    private void OpenProvidersSettings()
    {
        _mainTabs.SelectedTab = _settingsTab;
        _settingsControl.FocusProvidersTab();
    }

    private void UpdateProviderBadge()
    {
        var enabled = _workflow.ProviderState.Enabled;
        var reachable = _workflow.ProviderState.Reachable;
        var metrics = ProviderDiagnosticsHub.LatestMetrics;
        if (!enabled)
        {
            _providerBadge.Text = "⚠️ Local Provider: OFF";
            _providerBadge.LinkColor = Color.Goldenrod;
            _composer.SetProviderOffline(false);
            UpdateStatusLine();
            return;
        }

        if (!reachable)
        {
            _providerBadge.Text = "⚠️ Local Provider: OFFLINE";
            _providerBadge.LinkColor = Color.Firebrick;
            _composer.SetProviderOffline(true);
            UpdateStatusLine();
            return;
        }

        if (metrics.IsStale)
        {
            _providerBadge.Text = "⏳ Local Provider: STALE";
            _providerBadge.LinkColor = Color.Goldenrod;
        }
        else
        {
            _providerBadge.Text = "🧠 Local Provider: ON";
            _providerBadge.LinkColor = Color.SeaGreen;
        }
        _composer.SetProviderOffline(false);
        UpdateStatusLine();
    }

    private void OnProviderStateChanged(ProviderState state)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(new Action(UpdateProviderBadge));
    }

    private void OnProviderMetricsUpdated()
    {
        if (!IsHandleCreated) return;
        BeginInvoke(new Action(UpdateProviderBadge));
    }

    private void OnToolingDiagnosticsUpdated(ToolingDiagnostics diag)
    {
        _toolingDiagnostics = diag ?? ToolingDiagnostics.Empty;
        if (!IsHandleCreated) return;
        BeginInvoke(new Action(UpdateStatusLine));
    }

    private void UpdateStatusLine()
    {
        var enabled = _workflow.ProviderState.Enabled;
        var reachable = _workflow.ProviderState.Reachable;
        var metrics = ProviderDiagnosticsHub.LatestMetrics;
        var providerText = enabled
            ? (reachable
                ? (metrics.IsStale ? "Local Provider: STALE" : "Local Provider: ON")
                : "Local Provider: OFFLINE")
            : "Local Provider: OFF";
        var providerColor = enabled
            ? (reachable
                ? (metrics.IsStale ? Color.Goldenrod : Color.SeaGreen)
                : Color.Firebrick)
            : Color.Goldenrod;

        var toolchainStatus = ComputeToolchainStatus(_toolingDiagnostics);
        var toolchainText = $"Toolchain: {toolchainStatus.Text}";

        var executionText = $"Execution: {_config.General.ExecutionTarget}";
        var executionColor = Color.DimGray;

        _composer.UpdateStatusLine(providerText, providerColor, toolchainText, toolchainStatus.Color, executionText, executionColor);
    }

    private static (string Text, Color Color) ComputeToolchainStatus(ToolingDiagnostics diag)
    {
        if (diag == null) return ("WARN", Color.Goldenrod);
        if (diag.ValidationErrors != null && diag.ValidationErrors.Count > 0)
            return ("FAIL", Color.Firebrick);
        if (diag.MissingPrompts != null && diag.MissingPrompts.Count > 0)
            return ("WARN", Color.Goldenrod);
        if (diag.ToolCount == 0)
            return ("WARN", Color.Goldenrod);
        return ("OK", Color.SeaGreen);
    }

    private void OpenToolchainSettings()
    {
        _mainTabs.SelectedTab = _settingsTab;
        _settingsControl.FocusToolchainTab();
    }

    private void OpenGeneralSettings()
    {
        _mainTabs.SelectedTab = _settingsTab;
        _settingsControl.FocusGeneralTab();
    }

    private void NotifyProviderRetry()
    {
        AppendChat("Provider retry initiated.\n");
        UpdateProviderBadge();
    }

    private void TryLoadLastSession()
    {
        var sessions = _sessionStore.ListSessions();
        var last = sessions.FirstOrDefault();
        if (last != null)
            LoadSessionState(last);
    }

    private void LoadSessionState(SessionState state)
    {
        if (state == null) return;
        _workflow.ApplySessionState(state);

        _chatView.Clear();
        foreach (var msg in state.Messages ?? new List<SessionMessage>())
        {
            if (string.Equals(msg.Sender, "user", StringComparison.OrdinalIgnoreCase))
                AppendChat($"> {msg.Text}\n");
            else
                AppendChat(msg.Text + "\n");
        }

        var desired = (state.Attachments ?? new List<SessionAttachment>())
            .ToDictionary(a => a.StoredName, a => a.Active, StringComparer.OrdinalIgnoreCase);
        foreach (var item in _inbox.List())
        {
            if (desired.TryGetValue(item.StoredName, out var active))
                _inbox.SetActive(item.StoredName, active);
        }
        _composer.ReloadAttachments(_inbox.List());
        _workflow.SetAttachments(_inbox.List());
        UpdateStatusLine();
    }

    private void OnOutputCardProduced(OutputCard card)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<OutputCard>(OnOutputCardProduced), card);
            return;
        }

        _cards.Add(card);
        _cardList.Items.Add($"{card.Kind}: {card.Title}");
        if (_cardList.Items.Count > 0)
        {
            _cardList.SelectedIndex = _cardList.Items.Count - 1;
            ShowSelectedCard();
        }

        if (card.Kind == OutputCardKind.ProgramFile && !string.IsNullOrWhiteSpace(card.Title))
        {
            _filePreviews[card.Title] = card.Preview;
            AddPathToTree(card.Title);
        }
    }

    private void ShowSelectedCard()
    {
        if (_cardList.SelectedIndex < 0 || _cardList.SelectedIndex >= _cards.Count)
        {
            _cardDetail.Text = "";
            _downloadButton.Enabled = false;
            return;
        }

        var card = _cards[_cardList.SelectedIndex];
        _cardDetail.Text = card.ToDisplayText();
        _downloadButton.Enabled = card.Kind is OutputCardKind.Program or OutputCardKind.ProgramZip or OutputCardKind.ProgramTree;

        if (card.Kind == OutputCardKind.ProgramTree)
        {
            BuildTreeFromPreview(card.Preview);
        }
        else if (card.Kind == OutputCardKind.ProgramFile && !string.IsNullOrWhiteSpace(card.Title))
        {
            AddPathToTree(card.Title);
            _artifactPreview.Text = card.Preview;
        }
    }

    private void DownloadSelectedArtifact()
    {
        if (_cardList.SelectedIndex < 0 || _cardList.SelectedIndex >= _cards.Count)
            return;

        var card = _cards[_cardList.SelectedIndex];
        var zipPath = card.Metadata;
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            return;

        using var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(zipPath),
            Filter = "Zip archive|*.zip|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            File.Copy(zipPath, dialog.FileName, overwrite: true);
        }
    }

    private void BuildTreeFromPreview(string preview)
    {
        _artifactTree.BeginUpdate();
        _artifactTree.Nodes.Clear();

        var stack = new Stack<TreeNode>();
        var lines = (preview ?? "").Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedEnd = line.TrimEnd();
            var depth = line.TakeWhile(ch => ch == ' ').Count() / 2;
            var name = trimmedEnd.Trim();
            var isDir = name.EndsWith("/", StringComparison.Ordinal);
            name = name.TrimEnd('/');
            while (stack.Count > depth)
                stack.Pop();

            var path = stack.Count == 0 ? name : string.Join("/", stack.Reverse().Select(n => n.Text).Concat(new[] { name }));
            var node = new TreeNode(name) { Tag = path };
            if (stack.Count == 0)
                _artifactTree.Nodes.Add(node);
            else
                stack.Peek().Nodes.Add(node);

            if (isDir)
                stack.Push(node);
        }

        _artifactTree.ExpandAll();
        _artifactTree.EndUpdate();
    }

    private void AddPathToTree(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var parts = path.Replace("\\", "/").Split('/', StringSplitOptions.RemoveEmptyEntries);
        TreeNodeCollection current = _artifactTree.Nodes;
        var built = new List<string>();
        TreeNode? last = null;
        foreach (var part in parts)
        {
            built.Add(part);
            var existing = current.Cast<TreeNode>().FirstOrDefault(n => string.Equals(n.Text, part, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new TreeNode(part) { Tag = string.Join("/", built) };
                current.Add(existing);
            }
            last = existing;
            current = existing.Nodes;
        }

        if (last != null)
            _artifactTree.SelectedNode = last;
    }

    private void ShowArtifactPreview()
    {
        var node = _artifactTree.SelectedNode;
        if (node == null)
        {
            _artifactPreview.Text = "";
            return;
        }

        var path = node.Tag as string ?? node.Text;
        if (_filePreviews.TryGetValue(path, out var preview))
        {
            _artifactPreview.Text = preview;
            return;
        }

        _artifactPreview.Text = "";
    }

    private void ShowTracePane()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ShowTracePane));
            return;
        }
        _mainTabs.SelectedTab = _diagnosticsTab;
        _traceBox.Focus();
    }

    private void OnPlanReady(PlanDefinition def)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(new Action(() =>
        {
            using var preview = new PlanPreviewForm(def);
            if (preview.ShowDialog(this) == DialogResult.OK && preview.Result != null)
            {
                _workflow.ApplyEditedPlan(preview.Result.ToToolPlan());
                _workflow.ConfirmPlan();
            }
        }));
    }

}
