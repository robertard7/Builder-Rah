#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    private readonly Panel _clarifyPanel;
    private readonly Label _clarifyLabel;
    private readonly SplitContainer _chatSplit;
    private readonly ListBox _cardList;
    private readonly RichTextBox _cardDetail;

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

        buttonRow.Controls.Add(_stepLabel);
        buttonRow.Controls.Add(_runAllButton);
        buttonRow.Controls.Add(_runNextButton);
        buttonRow.Controls.Add(_stopButton);
        buttonRow.Controls.Add(_editPlanButton);
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
            Dock = DockStyle.Left,
            Width = 260,
            HorizontalScrollbar = true
        };
        _cardList.SelectedIndexChanged += (_, _) => ShowSelectedCard();

        _cardDetail = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            Font = new Font("Segoe UI", 9f)
        };

        var outputSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 200,
            SplitterDistance = 260
        };
        outputSplit.Panel1.Controls.Add(_cardList);
        outputSplit.Panel2.Controls.Add(_cardDetail);

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
            _cardList.SelectedIndex = _cardList.Items.Count - 1;
        _cardDetail.Text = card.ToDisplayText();
    }

    private void ShowSelectedCard()
    {
        if (_cardList.SelectedIndex < 0 || _cardList.SelectedIndex >= _cards.Count)
            return;

        _cardDetail.Text = _cards[_cardList.SelectedIndex].ToDisplayText();
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
