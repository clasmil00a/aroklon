using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;

namespace Aroklon.App.ViewModels;

public sealed partial class ScreenStubViewModel : ObservableObject
{
    private readonly Action _back;

    public ScreenStubViewModel(ScreenDefinition screen, Action back)
    {
        _back = back;
        ScreenId = screen.Id;
        Title = screen.Title;
        Purpose = screen.Purpose;
        Milestone = screen.Milestone;
    }

    public string ScreenId { get; }

    public string Title { get; }

    public string Purpose { get; }

    public string Milestone { get; }

    public string MilestoneLabel => "Dijadwalkan " + Milestone;

    [RelayCommand]
    private void Back() => _back();
}
