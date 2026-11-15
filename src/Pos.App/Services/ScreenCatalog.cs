namespace Pos.App.Services;

public sealed record ScreenDefinition(
    string Id,
    string Title,
    string Purpose,
    string Milestone)
{
    public string MilestoneLabel => "Dibangun di " + Milestone;
}

/// <summary>
/// Route table for every screen in the recon map. Screens that are not built
/// yet resolve to a readable stub instead of a dead link.
/// </summary>
public static class ScreenCatalog
{
    private static readonly ScreenDefinition[] Screens =
    [
        new("S01", "Masuk", "Autentikasi pengguna sebelum memulai shift.", "M0"),
        new("S02", "Kasir standar", "Layar kasir utama untuk transaksi cepat.", "M1"),
        new("S03", "Kasir visual", "Tata letak sentuh dengan grid produk.", "M7"),
        new("S04", "Cari produk", "Cari produk berdasarkan nama, kode, atau barcode.", "M1"),
        new("S05", "Jumlah & satuan", "Atur jumlah dan satuan jual untuk baris transaksi.", "M2"),
        new("S06", "Diskon", "Diskon per baris atau seluruh keranjang.", "M3"),
        new("S07", "Pembayaran", "Terima pembayaran dan hitung kembalian.", "M1"),
        new("S08", "Pembayaran terpisah", "Bayar dengan beberapa tipe pembayaran.", "M7"),
        new("S09", "Pengaturan pajak", "Ubah pajak untuk transaksi ini saja.", "M3"),
        new("S10", "Pilih pelanggan", "Lampirkan pelanggan ke transaksi.", "M2"),
        new("S11", "Aksi struk", "Cetak, email, simpan PDF, atau lewati struk.", "M1"),
        new("S12", "Pesanan terbuka", "Daftar transaksi yang ditahan dan dilanjutkan.", "M1"),
        new("S13", "Menu utama", "Pintu masuk ke seluruh bagian aplikasi.", "M0"),
        new("S14", "Manajemen", "Kerangka modul back-office.", "M0"),
        new("S15", "Daftar produk", "Kelola katalog dan grup produk.", "M5"),
        new("S16", "Editor produk", "Detail produk, harga, satuan, dan barcode.", "M2"),
        new("S17", "Grup produk", "Kelompokkan produk untuk pencarian.", "M5"),
        new("S18", "Daftar stok", "Lihat stok dan mulai penyesuaian.", "M5"),
        new("S19", "Riwayat stok", "Jejak pergerakan stok.", "M5"),
        new("S20", "Stok opname", "Hitung stok fisik dan posting selisih.", "M5"),
        new("S21", "Daftar pelanggan", "Kelola pelanggan dan pemasok.", "M5"),
        new("S22", "Editor pelanggan", "Data pelanggan dan daftar harga.", "M3"),
        new("S23", "Tarif pajak", "Tarif pajak dan pajak bawaan.", "M3"),
        new("S24", "Tipe pembayaran", "Tipe pembayaran dan perilakunya.", "M6"),
        new("S25", "Pengguna & akses", "Akun kasir, peran, dan izin.", "M6"),
        new("S26", "Detail perusahaan", "Identitas toko dan struk.", "M6"),
        new("S27", "Pengaturan", "Kerangka pengaturan aplikasi.", "M0"),
        new("S28", "Pengaturan umum", "Tampilan, bahasa, mata uang, dan hari kerja.", "M6"),
        new("S29", "Pesanan & pembayaran", "Perilaku pesanan, pembayaran, dan struk.", "M2"),
        new("S30", "Cetak & struk", "Printer dan isi struk.", "M6"),
        new("S31", "Kas masuk/keluar", "Catat uang masuk dan keluar dari laci.", "M4"),
        new("S32", "Riwayat penjualan", "Cari transaksi lama dan cetak ulang.", "M1"),
        new("S33", "Detail penjualan", "Detail transaksi dan pratinjau invoice.", "M4"),
        new("S34", "Refund", "Refund penuh atau sebagian.", "M4"),
        new("S35", "Tutup hari", "Hitung kas dan tutup register.", "M4"),
        new("S36", "Daftar laporan", "Pilih laporan dan filter.", "M4"),
        new("S37", "Pratinjau laporan", "Pratinjau, cetak, atau simpan laporan.", "M4"),
        new("S38", "Database & cadangan", "Lokasi database, cadangan, dan pemulihan.", "M6"),
        new("S39", "Daftar harga", "Daftar harga retail, grosir, dan per satuan.", "M2"),
        new("S40", "Editor daftar harga", "Buat, salin, atau ubah massal daftar harga.", "M2"),
        new("S41", "Harga produk", "Harga semua daftar untuk satu produk.", "M2"),
        new("S42", "Master satuan", "Nama satuan yang dipakai ulang.", "M2"),
        new("S43", "Grup produk", "Kelompokkan produk untuk pencarian.", "M5"),
        new("S44", "Laporan", "Laporan penjualan, stok, dan kasir.", "M8"),
        new("S45", "Penyesuaian stok", "Atur stok produk secara cepat (penyesuaian dan penerimaan).", "M9")
    ];

    private static readonly Dictionary<string, ScreenDefinition> ById =
        Screens.ToDictionary(screen => screen.Id, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ScreenDefinition> All => Screens;

    public static bool TryGet(string? id, out ScreenDefinition screen)
    {
        if (id is not null && ById.TryGetValue(id, out var found))
        {
            screen = found;
            return true;
        }

        screen = null!;
        return false;
    }

    public static ScreenDefinition Get(string id) =>
        ById.TryGetValue(id, out var screen)
            ? screen
            : new ScreenDefinition(id, id, "Layar tidak dikenal.", "M1");
}
