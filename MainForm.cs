#nullable enable
using System;
using System.Drawing;
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

    private readonly RichTextBox _chatView;

    private readonly TextBox _traceBox;
    private readonly TracePanelWriter _traceWriter;
    private readonly RunTrace _trace;

    private readonly WorkflowFacade _workflow;

    private readonly ChatComposerControl _composer;

    public MainForm()
    {
        Text = "RAH Builder (Reset: Settings-First)";
        Width = 1200;
        Height = 800;

        _config = ConfigStore.Load();

        _traceWriter = new TracePanelWriter();
        _trace = new RunTrace(new RahOllamaOnly.Tracing.TracePanelTraceSink(_traceWriter));

        _workflow = new WorkflowFacade(_trace);
        _workflow.UserFacingMessage += s => AppendChat(s + "\n");

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // Chat tab layout
        var chatRoot = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 820
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

        _composer = new ChatComposerControl();
        _composer.SendRequested += text => _ = SendNowAsync(text);

        leftPanel.Controls.Add(_chatView);
        leftPanel.Controls.Add(_composer);

        // Right side: trace
        _traceBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 9f)
        };

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

        chatRoot.Panel1.Controls.Add(leftPanel);
        chatRoot.Panel2.Controls.Add(_traceBox);

        tabs.TabPages.Add(new TabPage("Chat") { Controls = { chatRoot } });

        // Settings tab
        tabs.TabPages.Add(new TabPage("Settings")
        {
            Controls =
            {
                new SettingsHostControl(_config, AfterSettingsSaved) { Dock = DockStyle.Fill }
            }
        });

        Controls.Add(tabs);

        PublishConfigToRuntime();

        if (_config.General.EnableGlobalClipboardShortcuts)
            ClipboardPolicy.Apply(this);

        Shown += (_, _) => _composer.FocusInput();
        FormClosed += (_, _) => _workflow.UserFacingMessage -= OnUserFacingMessage;
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
}
