using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;

namespace Aroklon.App.ViewModels;

public sealed record BackOfficeModule(
    string ScreenId,
    string Title,
    string Purpose,
    string Milestone)
{
    public string MilestoneLabel => "Dibangun di " + Milestone;
}

public sealed partial class ManagementViewModel : ObservableObject
{
    private static readonly string[] ModuleScreens =
    [
        "S15", "S18", "S21", "S39", "S42", "S23", "S24", "S25", "S26", "S36"
    ];

    private readonly Action<string> _navigate;

    [ObservableProperty]
    private BackOfficeModule? _selectedModule;

    public ManagementViewModel(Action<string> navigate)
    {
        _navigate = navigate;
        Modules = ModuleScreens
            .Select(ScreenCatalog.Get)
            .Select(screen => new BackOfficeModule(
                screen.Id,
                screen.Title,
                screen.Purpose,
                screen.Milestone))
            .ToList();
        SelectedModule = Modules.FirstOrDefault();
    }

    public IReadOnlyList<BackOfficeModule> Modules { get; }

    [RelayCommand]
    private void OpenSettings() => _navigate("settings");
}
