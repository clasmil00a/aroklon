using System.Globalization;
using System.Text;
using Pos.Domain.Primitives;

namespace Pos.Domain.Documents;

public sealed record ReceiptLine(
    string Name,
    long QtyMilli,
    string UnitName,
    long UnitPriceMinor,
    long DiscountMinor,
    long TotalMinor);

public sealed record ReceiptPayment(
    string Name,
    long AmountMinor,
    long TenderedMinor,
    long ChangeMinor);

public sealed record ReceiptDocument(
    string CompanyName,
    string? Address,
    string? Phone,
    string? TaxNumber,
    string? LogoPath,
    string? Header,
    string? Footer,
    string Number,
    string DateText,
    string CashierName,
    string RegisterName,
    string? CustomerName,
    bool ShowUnits,
    IReadOnlyList<ReceiptLine> Lines,
    long SubtotalMinor,
    long DiscountMinor,
    long TaxMinor,
    long TotalMinor,
    long PaidMinor,
    long ChangeMinor,
    long BalanceMinor,
    IReadOnlyList<ReceiptPayment> Payments,
    string Currency = "IDR",
    int CurrencyMinorUnit = 0);

/// <summary>
/// Plain-text receipt used for thermal printers, text previews and saved
/// copies. Esc/POS wrapping and the Windows print dialog sit on top of this.
/// </summary>
public static class ReceiptRenderer
{
    public static string Render(ReceiptDocument receipt, int width = 42)
    {
        if (width < 28)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Lebar struk minimal 28 karakter.");
        }

        var text = new StringBuilder();
        void Center(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                text.AppendLine(CenterText(value, width));
            }
        }

        void Pair(string left, string right)
        {
            var leftText = left.Length > width - 2 ? left[..(width - 2)] : left;
            var space = Math.Max(1, width - leftText.Length - right.Length);
            text.AppendLine(leftText + new string(' ', space) + right);
        }

        void Separator() => text.AppendLine(new string('-', width));

        Center(receipt.CompanyName);
        Center(receipt.Address);
        if (!string.IsNullOrWhiteSpace(receipt.Phone))
        {
            Center("Telp. " + receipt.Phone);
        }

        if (!string.IsNullOrWhiteSpace(receipt.TaxNumber))
        {
            Center("NPWP " + receipt.TaxNumber);
        }

        Center(receipt.Header);
        Separator();
        Pair("No. " + receipt.Number, receipt.DateText);
        Pair("Kasir", receipt.CashierName);
        Pair("Register", receipt.RegisterName);
        if (!string.IsNullOrWhiteSpace(receipt.CustomerName))
        {
            Pair("Pelanggan", receipt.CustomerName);
        }

        Separator();

        foreach (var line in receipt.Lines)
        {
            text.AppendLine(Wrap(line.Name, width));
            var quantity = UnitMath.FormatQuantity(line.QtyMilli, receipt.ShowUnits ? line.UnitName : null);
            var unitPrice = MoneyMath.Format(line.UnitPriceMinor, receipt.CurrencyMinorUnit);
            Pair("  " + quantity + " x " + unitPrice, MoneyMath.Format(line.TotalMinor, receipt.CurrencyMinorUnit));
            if (line.DiscountMinor > 0)
            {
                Pair("  Diskon", "-" + MoneyMath.Format(line.DiscountMinor, receipt.CurrencyMinorUnit));
            }
        }

        Separator();
        Pair("Subtotal", MoneyMath.Format(receipt.SubtotalMinor, receipt.CurrencyMinorUnit));
        if (receipt.DiscountMinor > 0)
        {
            Pair("Diskon", "-" + MoneyMath.Format(receipt.DiscountMinor, receipt.CurrencyMinorUnit));
        }

        if (receipt.TaxMinor > 0)
        {
            Pair("Pajak", MoneyMath.Format(receipt.TaxMinor, receipt.CurrencyMinorUnit));
        }

        Pair("TOTAL", MoneyMath.Format(receipt.TotalMinor, receipt.CurrencyMinorUnit));

        if (receipt.Payments.Count > 0)
        {
            Separator();
            foreach (var payment in receipt.Payments)
            {
                Pair(payment.Name, MoneyMath.Format(payment.AmountMinor, receipt.CurrencyMinorUnit));
                if (payment.TenderedMinor > payment.AmountMinor)
                {
                    Pair("  Diterima", MoneyMath.Format(payment.TenderedMinor, receipt.CurrencyMinorUnit));
                }

                if (payment.ChangeMinor > 0)
                {
                    Pair("  Kembali", MoneyMath.Format(payment.ChangeMinor, receipt.CurrencyMinorUnit));
                }
            }
        }

        if (receipt.BalanceMinor > 0)
        {
            Pair("Sisa tagihan", MoneyMath.Format(receipt.BalanceMinor, receipt.CurrencyMinorUnit));
        }

        Separator();
        Center(receipt.Footer);
        text.AppendLine();
        Center("Barang yang sudah dibeli tidak dapat ditukar");
        Center("tanpa struk.");
        text.AppendLine();
        Center("Terima kasih");

        return text.ToString();
    }

    public static string FormatDate(DateTimeOffset local, string cultureName = "id-ID")
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }

        return local.ToString("dd/MM/yyyy HH:mm", culture);
    }

    private static string CenterText(string value, int width)
    {
        if (value.Length >= width)
        {
            return value[..width];
        }

        var left = (width - value.Length) / 2;
        return new string(' ', left) + value;
    }

    private static string Wrap(string value, int width)
    {
        if (value.Length <= width)
        {
            return value;
        }

        var builder = new StringBuilder();
        var remaining = value;
        while (remaining.Length > width)
        {
            var cut = remaining.LastIndexOf(' ', width);
            if (cut <= 0)
            {
                cut = width;
            }

            builder.AppendLine(remaining[..cut].TrimEnd());
            remaining = remaining[cut..].TrimStart();
        }

        builder.Append(remaining);
        return builder.ToString();
    }
}
