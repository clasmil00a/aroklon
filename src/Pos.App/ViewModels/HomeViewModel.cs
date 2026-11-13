using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Pos.App.ViewModels;

public sealed record HomeMenuItem(
    string Title,
    string Description,
    string Route,
    string Badge);

public sealed record HomeStatusItem(
    string Label,
    string Value,
    bool IsReady);

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly Action<string> _navigate;
    private readonly Action _fullScreen;
    private readonly Func<Task> _signOut;
    private readonly Action _exit;

    public HomeViewModel(
        string firstName,
        Action<string> navigate,
        Action fullScreen,
        Func<Task> signOut,
        Action exit,
        string version)
    {
        _navigate = navigate;
        _fullScreen = fullScreen;
        _signOut = signOut;
        _exit = exit;

        Greeting = "Selamat datang, " + firstName;
        Today = FormatToday();
        Version = version;

        MenuItems =
        [
            new("Kasir", "Mulai transaksi, cari produk, dan terima pembayaran.", "S02", "M1"),
            new("Riwayat penjualan", "Cari transaksi lama dan cetak ulang struk.", "S32", "M1"),
            new("Pesanan terbuka", "Lanjutkan transaksi yang ditahan sebelumnya.", "S12", "M1"),
            new("Master satuan", "bungkus, renteng, dus, kg, dan konversi lainnya.", "S42", "M2"),
            new("Daftar produk", "Lihat dan pilih produk untuk diedit satuan jualnya.", "S15", "M2"),
            new("Daftar harga", "Kelola daftar harga retail, grosir, dan per satuan.", "S39", "M2"),
            new("Harga produk", "Lihat harga semua daftar untuk satu produk.", "S41", "M2"),
            new("Pelanggan", "Pilih pelanggan dan daftar harga defaultnya.", "S10", "M2"),
            new("Kas masuk/keluar", "Catat uang masuk dan keluar dari laci kas.", "S31", "M4"),
            new("Refund", "Refund transaksi dan kembalikan stok.", "S34", "M4"),
            new("Tutup hari", "Hitung kas, cetak laporan Z, lalu tutup register.", "S35", "M4"),
            new("Kas masuk/keluar", "Catat uang masuk dan keluar dari laci kas.", "S31", "M4"),
            new("Tutup hari", "Hitung kas, cetak laporan, lalu tutup register.", "S35", "M4"),
            new("Manajemen", "Produk, stok, pelanggan, harga, dan laporan.", "management", "S14"),
            new("Pengaturan", "Tema, perilaku kasir, cetak, dan database.", "settings", "S27")
        ];

        StatusItems =
        [
            new("Database", "Siap · skema v1", true),
            new("Cadangan", "Otomatis saat aplikasi dibuka", true),
            new("Multi-satuan", "Disiapkan pada M2", false),
            new("Daftar harga", "Disiapkan pada M2", false)
        ];
    }

    public string Greeting { get; }

    public string Today { get; }

    public string Version { get; }

    public IReadOnlyList<HomeMenuItem> MenuItems { get; }

    public IReadOnlyList<HomeStatusItem> StatusItems { get; }

    [RelayCommand]
    private void StartSale() => _navigate("S02");

    [RelayCommand]
    private void OpenMenuItem(HomeMenuItem? item)
    {
        if (item is not null)
        {
            _navigate(item.Route);
        }
    }

    [RelayCommand]
    private void FullScreen() => _fullScreen();

    [RelayCommand]
    private async Task SignOutAsync() => await _signOut();

    [RelayCommand]
    private void Exit() => _exit();

    private static string FormatToday()
    {
        try
        {
            return DateTime.Now.ToString(
                "dddd, d MMMM yyyy",
                CultureInfo.GetCultureInfo("id-ID"));
        }
        catch (CultureNotFoundException)
        {
            return DateTime.Now.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
        }
    }
}
