using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Pos.App.ViewModels;

public sealed record BackOfficeModule(
    string ScreenId,
    string Title,
    string Description,
    string Icon);

public sealed partial class ManagementViewModel : ObservableObject
{
    private readonly Action<string> _navigate;

    public ManagementViewModel(Action<string> navigate)
    {
        _navigate = navigate;
        Modules =
        [
            new("S15", "Produk", "Tambah, ubah, dan atur harga produk.", "\uE7BF"),
            new("S43", "Grup produk", "Kelompokkan produk untuk laporan dan pencarian.", "\uE8F4"),
            new("S45", "Penyesuaian stok", "Terima barang dan koreksi stok.", "\uE7B8"),
            new("S10", "Pelanggan", "Data pelanggan dan daftar harga khusus.", "\uE77B"),
            new("S39", "Daftar harga", "Harga retail, grosir, dan per satuan.", "\uE8EC"),
            new("S23", "Tarif pajak", "Pajak yang dipakai pada transaksi.", "\uE9D9"),
            new("S24", "Tipe pembayaran", "Tunai, kartu, kredit, dan perilakunya.", "\uE8C7"),
            new("S25", "Pengguna & akses", "Akun kasir, peran, dan izin.", "\uE779"),
            new("S44", "Laporan", "Penjualan, stok, dan kas register.", "\uE9D2"),
            new("S42", "Master satuan", "Satuan yang dipakai ulang oleh produk.", "\uE8EF"),
            new("S38", "Database & cadangan", "Lokasi database dan pemulihan data.", "\uE8F1"),
            new("settings", "Pengaturan", "Tampilan, printer, dan preferensi toko.", "\uE713")
        ];
    }

    public IReadOnlyList<BackOfficeModule> Modules { get; }

    [RelayCommand]
    private void OpenModule(BackOfficeModule? module)
    {
        if (module is not null)
        {
            _navigate(module.ScreenId);
        }
    }

    [RelayCommand]
    private void OpenSettings() => _navigate("settings");

    [RelayCommand]
    private void OpenReports() => _navigate("S44");

    [RelayCommand]
    private void OpenHistory() => _navigate("S32");

    [RelayCommand]
    private void OpenProducts() => _navigate("S15");

    [RelayCommand]
    private void GoBack() => _navigate("S02");
}
