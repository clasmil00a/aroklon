using CommunityToolkit.Mvvm.ComponentModel;

namespace Aroklon.App.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(string route, string label)
    {
        Route = route;
        Label = label;
    }

    public string Route { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isActive;
}
