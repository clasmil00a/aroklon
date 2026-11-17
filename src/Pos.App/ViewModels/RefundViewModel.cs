using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.App.Services;
using Pos.Data.Services;
using Pos.Domain;
using Pos.Domain.Models;
using Pos.Domain.Pos;
using Serilog;

namespace Pos.App.ViewModels;

public sealed record RefundSaleLine(
    string SaleItemId,
    string Name,
    string UnitName,
    long QtyMilli,
    long RefundedQtyMilli,
    long RefundableQtyMilli,
    long UnitPriceMinor,
    long LineTotalMinor,
    long RefundQtyMilli = 0);

public sealed partial class RefundViewModel : ObservableObject
{
    private readonly RefundService _refunds;
    private readonly SaleService _sales;

    [ObservableProperty]
    private string _saleNumberQuery = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _infoMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _saleFound;

    [ObservableProperty]
    private string _originalSaleNumber = string.Empty;

    [ObservableProperty]
    private string _originalSaleTotal = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RefundSaleLine> _refundLines = new();

    [ObservableProperty]
    private string _paymentTypeId = "pmt-cash";

    [ObservableProperty]
    private long _tenderedAmount;

    [ObservableProperty]
    private string _refundReason = string.Empty;

    private string? _originalSaleId;

    public RefundViewModel(RefundService refunds, SaleService sales)
    {
        _refunds = refunds;
        _sales = sales;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    [RelayCommand]
    private async Task SearchSaleAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SaleNumberQuery))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        SaleFound = false;
        RefundLines = new ObservableCollection<RefundSaleLine>();
        _originalSaleId = null;

        try
        {
            // Search sales by number.
            var history = await _sales.ListAsync(new SaleFilter
            {
                Query = SaleNumberQuery.Trim(),
                Limit = 10
            });

            var sale = history.FirstOrDefault(s =>
                s.Number.Equals(SaleNumberQuery.Trim(), System.StringComparison.OrdinalIgnoreCase));

            if (sale is null)
            {
                ErrorMessage = "Transaksi tidak ditemukan.";
                return;
            }

            if (sale.Status == "voided")
            {
                ErrorMessage = "Transaksi yang dibatalkan tidak dapat direfund.";
                return;
            }

            _originalSaleId = sale.Id;
            OriginalSaleNumber = sale.Number;
            OriginalSaleTotal = Pos.Domain.Primitives.MoneyMath.Format(sale.TotalMinor);
            SaleFound = true;

            // Load sale details to get line items.
            var detail = await _sales.GetAsync(sale.Id);
            foreach (var line in detail.Lines.Where(l => l.Status != "voided"))
            {
                var refundable = line.QtyMilli; // Simplified: assume no prior refunds for M4
                RefundLines.Add(new RefundSaleLine(
                    line.Id, line.Name, line.UnitName, line.QtyMilli, 0, refundable,
                    line.UnitPriceMinor, line.TotalMinor));
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to search sale for refund");
            ErrorMessage = "Gagal mencari transaksi. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitRefundAsync()
    {
        if (IsBusy || _originalSaleId is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        var refundLines = RefundLines.Where(l => l.RefundQtyMilli > 0).ToList();
        if (refundLines.Count == 0)
        {
            ErrorMessage = "Pilih minimal satu baris dengan jumlah refund lebih dari nol.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RefundReason))
        {
            ErrorMessage = "Alasan refund wajib diisi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PaymentTypeId))
        {
            ErrorMessage = "Tipe pembayaran wajib dipilih.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _refunds.CreateAsync(new(
                _originalSaleId,
                refundLines.Select(l => new RefundLineInput(
                    l.SaleItemId, l.RefundQtyMilli, null)).ToList(),
                PaymentTypeId,
                TenderedAmount,
                RefundReason.Trim()));

            InfoMessage = $"Refund {result.RefundNumber} berhasil. Total refund " +
                Pos.Domain.Primitives.MoneyMath.Format(result.RefundTotalMinor) +
                (result.ChangeMinor > 0 ? $" · Kembali {Pos.Domain.Primitives.MoneyMath.Format(result.ChangeMinor)}" : string.Empty);

            SaleFound = false;
            RefundLines = new ObservableCollection<RefundSaleLine>();
            _originalSaleId = null;
            SaleNumberQuery = string.Empty;
            TenderedAmount = 0;
            RefundReason = string.Empty;
        }
        catch (ValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to process refund");
            ErrorMessage = "Gagal memproses refund. Coba lagi.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetRefundQty(RefundSaleLine? line, long qty)
    {
        if (line is null || IsBusy)
        {
            return;
        }

        if (qty < 0 || qty > line.RefundableQtyMilli)
        {
            return;
        }

        var index = RefundLines.IndexOf(line);
        RefundLines[index] = line with { RefundQtyMilli = qty };
    }

    [RelayCommand]
    private void RefundFull(RefundSaleLine? line) => SetRefundQty(line, line?.RefundableQtyMilli ?? 0);

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    public event Action? BackRequested;
}
