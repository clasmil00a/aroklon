using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aroklon.App.Services;
using Pos.Data.Services;
using Pos.Domain.Documents;
using Pos.Domain.Models;
using Serilog;

namespace Aroklon.App.ViewModels;

public sealed partial class ReceiptViewModel : ObservableObject
{
    private readonly SaleService _sales;

    [ObservableProperty]
    private ReceiptDocument? _document;

    [ObservableProperty]
    private string _receiptText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private string _saleId = string.Empty;

    public ReceiptViewModel(SaleService sales)
    {
        _sales = sales;
    }

    public void SetSale(string saleId) => _saleId = saleId;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    public async Task InitializeAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(_saleId))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var document = await _sales.GetReceiptAsync(_saleId);
            Document = document;
            ReceiptText = SaleService.RenderReceipt(document);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load receipt");
            ErrorMessage = "Gagal memuat struk. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Document is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // Print action: send to default printer (stub for M5 - actual ESC/POS wiring in M6).
            Log.Information("Printing receipt {Number}:\n{Text}", Document.Number, ReceiptText);
            InfoMessage = "Struk telah dikirim ke printer (cetak tidak terhubung ke printer fisik sampai M6).";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Print failed");
            ErrorMessage = "Cetak gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        if (Document is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // Save PDF action: save receipt text to file (stub for M5).
            var fileName = $"struk-{Document.Number.Replace('/', '-')}.txt";
            var filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            await System.IO.File.WriteAllTextAsync(filePath, ReceiptText);
            Log.Information("Receipt saved to {Path}", filePath);
            InfoMessage = "Struk disimpan ke " + filePath;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Save PDF failed");
            ErrorMessage = "Simpan gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EmailAsync()
    {
        if (Document is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // Email action: stub for M5 - actual email integration in M6.
            Log.Information("Emailing receipt {Number} to customer", Document.Number);
            InfoMessage = "Struk akan dikirim via email (email tidak terhubung sampai M6).";
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Email failed");
            ErrorMessage = "Email gagal. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SkipAsync()
    {
        InfoMessage = "Struk dilewati.";
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
