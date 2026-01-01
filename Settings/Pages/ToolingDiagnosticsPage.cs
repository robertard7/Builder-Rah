#nullable enable
using System.Windows.Forms;

namespace RahBuilder.Settings.Pages;

using RahBuilder.Settings;

public sealed class ToolingDiagnosticsPage : UserControl, ISettingsPageProvider
{
    public new string Name => "Tooling Diagnostics";

    private readonly AppConfig _config;

    public ToolingDiagnosticsPage(AppConfig config)
    {
        _config = config;
        Dock = DockStyle.Fill;

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Diagnostics placeholder (wire real counts + validation state next).",
            AutoSize = false
        });
    }

    public Control BuildPage(AppConfig config) => new ToolingDiagnosticsPage(config);
}
