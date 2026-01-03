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
using RahBuilder.Ui;
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
    private readonly List<OutputCard> _cards = new();
    private readonly TableLayoutPanel _stepPanel;
    private readonly Label _planSummaryLabel;
    private readonly Label _stepLabel;
    private readonly Button _runAllButton;
    private readonly Button _runNextButton;
    private readonly Button _stopButton;
    private readonly Button _editPlanButton;
    private readonly Button _previewPlanButton;
    private readonly Button _downloadButton;
    private readonly Panel _clarifyPanel;
    private readonly Label _clarifyLabel;
    private readonly SplitContainer _chatSplit;
    private readonly ListBox _cardList;
    private readonly RichTextBox _cardDetail;
    private readonly TreeView _artifactTree;
    private readonly RichTextBox _artifactPreview;
    private readonly Dictionary<string, string> _filePreviews = new(StringComparer.OrdinalIgnoreCase);

    public MainForm()
    {
        Text = "RAH Builder (Reset: Settings-First)";
        Width = 1200;
        Height = 800;

        _traceWriter = new TracePanelWriter();
        _trace = new RunTrace(new RahOllamaOnly.Tracing.TracePanelTraceSink(_traceWriter));

        _config = ConfigStore.Load();
        _inbox = new AttachmentInbox(_config.General, _trace);

        _workflow = new WorkflowFacade(_trace);
        _workflow.UserFacingMessage += OnUserFacingMessage;
        _workflow.StatusChanged += UpdateStatus;
        _workflow.PendingQuestionChanged += UpdateClarify;
        _workflow.PendingStepChanged += UpdateStepPanel;
        _workflow.OutputCardProduced += OnOutputCardProduced;
        _workflow.TraceAttentionRequested += ShowTracePane;
        _workflow.PlanReady += OnPlanReady;

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // Chat tab layout
        _chatSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 820,
            Panel2Collapsed = true
        };

        // Left side: transcript + composer
        var leftPanel = new Panel { Dock = DockStyle.Fill };

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
        _workflow.SetAttachments(_inbox.List());

        var topButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };

        var toggleTrace = new Button { Text = "Trace", AutoSize = true };
        toggleTrace.Click += (_, _) => ToggleTracePane();

        var demoAttachments = new Button { Text = "Demo Attachments", AutoSize = true };
        demoAttachments.Click += (_, _) => CreateDemoAttachments();

        var demoRequest = new Button { Text = "Demo Request", AutoSize = true };
        demoRequest.Click += async (_, _) => await SendNowAsync("Read the note and describe the picture.").ConfigureAwait(true);

        var demoIntent = new Button { Text = "Intent Demo Flow", AutoSize = true };
        demoIntent.Click += async (_, _) => await RunIntentDemoAsync().ConfigureAwait(true);
        var demoEdgeCase = new Button { Text = "Intent Edge Case", AutoSize = true };
        demoEdgeCase.Click += async (_, _) => await RunEdgeCaseAsync().ConfigureAwait(true);

        topButtons.Controls.Add(toggleTrace);
        topButtons.Controls.Add(demoAttachments);
        topButtons.Controls.Add(demoRequest);
        topButtons.Controls.Add(demoIntent);
        topButtons.Controls.Add(demoEdgeCase);

        _clarifyPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(6),
            BackColor = Color.FromArgb(255, 250, 230),
            Visible = false
        };
        _clarifyLabel = new Label { AutoSize = true, Dock = DockStyle.Fill };
        _clarifyPanel.Controls.Add(_clarifyLabel);

        _stepPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6),
            Visible = false,
            BackColor = Color.FromArgb(235, 242, 255)
        };
        _stepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _planSummaryLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Padding = new Padding(0, 0, 0, 6)
        };
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        _stepLabel = new Label { AutoSize = true, Padding = new Padding(0, 4, 8, 0) };
        _runAllButton = new Button { Text = "Run All", AutoSize = true };
        _runAllButton.Click += async (_, _) => await RunAllStepsAsync().ConfigureAwait(true);
        _runNextButton = new Button { Text = "Run Next", AutoSize = true };
        _runNextButton.Click += async (_, _) => await RunNextStepAsync().ConfigureAwait(true);
        _stopButton = new Button { Text = "Stop", AutoSize = true };
        _stopButton.Click += (_, _) => StopPlan();
        _editPlanButton = new Button { Text = "Modify Plan", AutoSize = true };
        _editPlanButton.Click += (_, _) => EditPlan();
        _previewPlanButton = new Button { Text = "Preview Plan", AutoSize = true };
        _previewPlanButton.Click += (_, _) => PreviewPlan();

        buttonRow.Controls.Add(_stepLabel);
        buttonRow.Controls.Add(_runAllButton);
        buttonRow.Controls.Add(_runNextButton);
        buttonRow.Controls.Add(_stopButton);
        buttonRow.Controls.Add(_editPlanButton);
        buttonRow.Controls.Add(_previewPlanButton);
        _stepPanel.Controls.Add(_planSummaryLabel, 0, 0);
        _stepPanel.Controls.Add(buttonRow, 0, 1);

        leftPanel.Controls.Add(_chatView);
        leftPanel.Controls.Add(_composer);
        leftPanel.Controls.Add(_stepPanel);
        leftPanel.Controls.Add(_clarifyPanel);
        leftPanel.Controls.Add(topButtons);
        _composer.BringToFront();
        _chatView.SendToBack();
        _stepPanel.BringToFront();
        _clarifyPanel.BringToFront();
        topButtons.BringToFront();

        // Right side: trace + output cards
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
        auxTabs.TabPages.Add(new TabPage("Trace") { Controls = { _traceBox } });
        auxTabs.TabPages.Add(new TabPage("Outputs") { Controls = { outputSplit } });

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

        _chatSplit.Panel1.Controls.Add(leftPanel);
        _chatSplit.Panel2.Controls.Add(auxTabs);

        tabs.TabPages.Add(new TabPage("Chat") { Controls = { _chatSplit } });

        // Settings tab
        tabs.TabPages.Add(new TabPage("Settings")
        {
            Controls =
            {
                new SettingsHostControl(_config, AfterSettingsSaved, _trace) { Dock = DockStyle.Fill }
            }
        });

        Controls.Add(tabs);

        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _modeStatus = new ToolStripStatusLabel("Mode: -");
        _repoStatus = new ToolStripStatusLabel("Repo: -");
        _attachmentsStatus = new ToolStripStatusLabel("Attachments: 0/0");
        _workflowStatus = new ToolStripStatusLabel("Workflow: Idle");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _modeStatus, _repoStatus, _attachmentsStatus, _workflowStatus });
        Controls.Add(_statusStrip);

        PublishConfigToRuntime();

        if (_config.General.EnableGlobalClipboardShortcuts)
            ClipboardPolicy.Apply(this);

        Shown += (_, _) => _composer.FocusInput();
        FormClosed += (_, _) =>
        {
            _workflow.UserFacingMessage -= OnUserFacingMessage;
            _workflow.StatusChanged -= UpdateStatus;
            _workflow.PendingQuestionChanged -= UpdateClarify;
            _workflow.PendingStepChanged -= UpdateStepPanel;
            _workflow.OutputCardProduced -= OnOutputCardProduced;
            _workflow.TraceAttentionRequested -= ShowTracePane;
            _workflow.PlanReady -= OnPlanReady;
        };

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

    private void UpdateClarify(string? question)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string?>(UpdateClarify), question);
            return;
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            _clarifyPanel.Visible = false;
            _clarifyLabel.Text = "";
            return;
        }

        _clarifyLabel.Text = question;
        _clarifyPanel.Visible = true;
    }

    private void UpdateStepPanel(string? summary, bool hasPlan)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string?, bool>(UpdateStepPanel), summary, hasPlan);
            return;
        }

        _planSummaryLabel.Text = summary ?? "";
        _stepLabel.Text = ExtractNextStep(summary);
        _stepPanel.Visible = hasPlan && !string.IsNullOrWhiteSpace(summary);
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

    private void ToggleTracePane()
    {
        _chatSplit.Panel2Collapsed = !_chatSplit.Panel2Collapsed;
        if (_chatSplit.Panel2Collapsed)
        {
            _composer.FocusInput();
        }
        else
        {
            _traceBox.Focus();
        }
    }

    private void ShowTracePane()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ShowTracePane));
            return;
        }
        _chatSplit.Panel2Collapsed = false;
        _traceBox.Focus();
    }

    private async Task RunNextStepAsync()
    {
        try
        {
            await _workflow.ApproveNextStepAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendChat("[error] " + ex.Message + "\n");
            ShowTracePane();
        }
    }

    private void StopPlan()
    {
        _workflow.StopPlan();
        UpdateStepPanel(null, false);
    }

    private async Task RunAllStepsAsync()
    {
        try
        {
            await _workflow.ApproveAllStepsAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendChat("[error] " + ex.Message + "\n");
            ShowTracePane();
        }
    }

    private void EditPlan()
    {
        var plan = _workflow.GetPendingPlan();
        if (plan == null)
        {
            _workflow.RequestPlanEdit();
            UpdateStepPanel(null, false);
            return;
        }

        using var editor = new PlanEditorForm(plan);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Result != null)
        {
            _workflow.ApplyEditedPlan(editor.Result);
        }
    }

    private void PreviewPlan()
    {
        var plan = _workflow.GetPendingPlan();
        if (plan == null) return;

        var definition = PlanDefinition.FromToolPlan("", plan);
        using var preview = new PlanPreviewForm(definition);
        if (preview.ShowDialog(this) == DialogResult.OK && preview.Result != null)
        {
            _workflow.ApplyEditedPlan(preview.Result.ToToolPlan());
            _workflow.ConfirmPlan();
        }
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

    private void CreateDemoAttachments()
    {
        try
        {
            Directory.CreateDirectory(_inbox.InboxPath);
            var notePath = Path.Combine(_inbox.InboxPath, "demo_note.txt");
            var imgPath = Path.Combine(_inbox.InboxPath, "demo_image.png");
            File.WriteAllText(notePath, "This is a short demo note.\nIt proves the attachment pipeline works.");

            using (var bmp = new Bitmap(64, 64))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightSteelBlue);
                g.FillEllipse(Brushes.DarkSlateBlue, 10, 10, 44, 44);
                bmp.Save(imgPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            var result = _inbox.AddFiles(new[] { notePath, imgPath });
            if (!result.Ok)
            {
                AppendChat("[error] " + result.Message + "\n");
                return;
            }

            _composer.ReloadAttachments(_inbox.List());
            _workflow.SetAttachments(_inbox.List());
            AppendChat("Demo attachments added.\n");
        }
        catch (Exception ex)
        {
            AppendChat("[error] " + ex.Message + "\n");
        }
    }

    private async Task RunIntentDemoAsync()
    {
        CreateDemoAttachments();
        await SendNowAsync("Describe this image and document and combine results.").ConfigureAwait(true);
    }

    private async Task RunEdgeCaseAsync()
    {
        _composer.ReloadAttachments(Array.Empty<AttachmentInbox.AttachmentEntry>());
        _workflow.SetAttachments(Array.Empty<AttachmentInbox.AttachmentEntry>());
        await SendNowAsync("Summarize the attached file and image.").ConfigureAwait(true);
    }

    private static string ExtractNextStep(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return "";

        var lines = summary.Replace("\r", "").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("->", StringComparison.OrdinalIgnoreCase))
                return "Next: " + trimmed.TrimStart('-', '>').Trim();
        }

        return "";
    }
}
