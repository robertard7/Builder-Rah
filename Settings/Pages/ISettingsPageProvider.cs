#nullable enable
using System.Windows.Forms;

namespace RahBuilder.Settings;

/// <summary>
/// Implemented by settings pages that can build a WinForms control.
/// </summary>
public interface ISettingsPageProvider
{
    /// <summary>
    /// Display name shown in the Settings tab header.
    /// (Yes, this collides with Control.Name if you put it on a Control. Use `new` on the implementation.)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Build the page UI bound to the current config instance.
    /// </summary>
    Control BuildPage(AppConfig config);
}
