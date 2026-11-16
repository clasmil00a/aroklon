namespace Pos.App.ViewModels;

/// <summary>
/// One row in the Aronium-style main menu flyout.
/// Section is shown above the row when it is not empty.
/// </summary>
public sealed record ShellMenuItem(
    string Label,
    string Route,
    string Icon,
    string Section = "",
    string Shortcut = "",
    bool SeparatorBefore = false);
