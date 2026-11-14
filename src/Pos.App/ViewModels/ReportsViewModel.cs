using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain.Models;
using Serilog;
using System.Collections.ObjectModel;

namespace Pos.App.ViewModels;

public sealed partial class ReportsViewModel : ObservableObject
{
    private readonly ReportsService _reportsService;

    [ObservableProperty]
    private ObservableCollection<ReportDefinition> _reports = new();

    [ObservableProperty]
    private string _selectedReportId = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _reportContent = string.Empty;

    public ReportsViewModel(ReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var reports = await _reportsService.ListReportsAsync();
            Reports = new ObservableCollection<ReportDefinition>(reports);
            SelectedReportId = reports.FirstOrDefault()?.Id ?? string.Empty;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load reports");
            ErrorMessage = "Gagal memuat laporan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedReportId)) return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        ReportContent = string.Empty;

        try
        {
            var reportContent = await _reportsService.GenerateReportAsync(new ReportParameters(
                SelectedReportId,
                null, null, null, null, null, string.Empty
            ));

            ReportContent = reportContent;
            InfoMessage = "Laporan berhasil digenerate.";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to generate report");
            ErrorMessage = "Gagal generate laporan. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}