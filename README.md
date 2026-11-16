# Aroklon

Aplikasi kasir Windows untuk toko sendiri. WPF native, database SQLite lokal,
tanpa server dan tanpa Chromium. Antarmuka berbahasa Indonesia dengan sapaan
formal “Anda”.

Status: aplikasi kasir siap dipakai untuk penjualan harian. Menu yang belum
tersedia membuka halaman status, bukan tautan mati.

## Persyaratan

- Windows 10 atau Windows 11, 64-bit.
- Untuk membangun: .NET SDK 10.0.4xx.
- Untuk menjalankan hasil build framework-dependent: .NET Desktop Runtime 10.
- Untuk pengembangan di Linux: kompilasi WPF didukung dengan
  `EnableWindowsTargeting=true`, tetapi aplikasi tidak dapat dijalankan atau
  diuji secara visual.

## Build dan jalankan

Di Windows:

```powershell
dotnet build src/Pos.App/Pos.App.csproj
dotnet run --project src/Pos.App/Pos.App.csproj
```

Di Linux (kompilasi saja):

```bash
export PATH=/root/.dotnet:$PATH
dotnet build src/Pos.App/Pos.App.csproj
```

## Tes

```bash
dotnet test tests/Pos.Domain.Tests/Pos.Domain.Tests.csproj
dotnet test tests/Pos.Data.Tests/Pos.Data.Tests.csproj
```

Tes UI berada di `tests/Pos.UiTests` dan baru dapat dijalankan di Windows.
Tes data memakai database sementara dan Argon2 biaya rendah; produksi memakai
Argon2id 64 MB, 3 iterasi, paralelisme 2.

## Yang sudah ada di M0

- Onboarding pertama: nama toko, akun administrator, database dibuat otomatis.
- Login dengan Argon2id, sesi di memori, lockout 5 percobaan / 15 menit.
- Shell aplikasi bergaya Aronium: bilah atas dan menu utama berupa flyout di
  sisi kiri, dengan pintasan papan tombol.
- Kasir standar: pencarian produk dan barcode, keranjang yang dapat digulir,
  jumlah, diskon, komentar, ubah harga, parkir/pulihkan pesanan, pembayaran
  tunai/non-tunai, kembalian, dan struk.
- Manajemen: produk baru/edit, varian satuan dan harga, grup produk, stok,
  pelanggan, daftar harga, pajak, tipe pembayaran, pengguna, laporan, satuan,
  serta database dan cadangan.
- Pengaturan toko, printer struk, tema, dan impor data Aronium Lite.
- Tema gelap (bawaan) dan terang, tersimpan per pengguna Windows.
- Aplikasi terbuka dalam jendela maksimal secara bawaan, sehingga title bar
  dan taskbar Windows tetap terlihat. F11 atau tombol Ukuran jendela
  mengganti antara jendela maksimal dan jendela normal.
- Layar masuk dapat mengingat nama pengguna terakhir (kata sandi tidak pernah
  disimpan).
- Cadangan harian `VACUUM INTO` saat start, retensi 30 file, tombol cadangan
  manual di Pengaturan.
- Audit append-only, pemeriksaan integritas SQLite saat start, single-instance
  mutex, dan log Serilog.

## Lokasi data

Semua data pengguna berada di profil Windows, bukan di folder instalasi:

```text
%LOCALAPPDATA%\Aroklon\
  data\pos.db
  backups\pos-YYYYMMDD-HHmmss.db
  logs\aroklon-YYYYMMDD.log
  ui.json
```

`ui.json` menyimpan pilihan tema, nama pengguna terakhir (opsional), dan
preferensi jendela maksimal. Pengaturan toko ada di tabel `settings` di SQLite.

## Impor dari Aronium

Aronium Lite menyimpan seluruh data di satu file SQLite bernama `pos.db`.
Aplikasi dapat membacanya langsung tanpa mengubah file sumber:

1. Tutup Aronium Lite.
2. Salin file dari `%LOCALAPPDATA%\Aronium\Data\pos.db` ke folder lain bila
   ingin menyimpan cadangan.
3. Di aplikasi ini buka **Pengaturan > Database > Impor dari Aronium Lite**.
4. Pilih `pos.db`, klik **Periksa**, lalu tinjau jumlah dan peringatan.
5. Pilih data yang ingin digabungkan dan klik **Impor data Aronium**.

Yang diimpor: profil toko, grup produk (termasuk subgrup), produk, satuan
dasar, barcode, pajak persentase, pelanggan/pemasok, tipe pembayaran, dan
opsional stok awal. Data lama tidak dihapus. SKU, barcode, nama, atau kode
pelanggan yang sama akan diperbarui bila opsi **Perbarui data yang sudah ada**
dicentang, sehingga impor dapat diulang tanpa membuat duplikat.

Yang belum diimpor: riwayat transaksi Aronium, pajak nominal tetap, diskon
khusus pelanggan, dan foto produk. Stok awal hanya dimasukkan untuk produk
yang belum memiliki pergerakan stok di aplikasi ini, agar stok tidak terhitung
dua kali.

Aronium Pro memakai SQL Server, jadi file cadangan `.bak` tidak dapat dibaca
langsung sebagai SQLite. Untuk Aronium Pro, ekspor produk dan pelanggan ke CSV
dari Aronium sebagai cadangan untuk migrasi manual; impor CSV belum tersedia
di aplikasi ini.

## Arsitektur

| Proyek | Isi |
| --- | --- |
| `src/Pos.Domain` | Uang, jumlah, satuan, resolusi harga, izin, aturan kata sandi, model, error |
| `src/Pos.Data` | SQLite, migrasi, layanan auth/pengaturan/perusahaan/pengguna/cadangan |
| `src/Pos.App` | WPF, tema, view model, navigasi, host/DI/Serilog |
| `tests/Pos.Domain.Tests` | Tes murni domain |
| `tests/Pos.Data.Tests` | Tes database dan layanan |
| `tests/Pos.UiTests` | Kerangka tes UI Windows |

Keputusan utama:

- .NET 10 LTS + WPF (`net10.0-windows`), bukan Electron dan bukan EF Core.
- SQLite + Dapper + SQL mentah; migrasi SQL tertanam.
- Uang dalam satuan minor integer; jumlah dalam seperseribu; faktor satuan
  juga seperseribu.
- Stok selalu dalam satuan terkecil. Satuan jual hanya mengonversi di tepi.
- Harga daftar eksplisit menimpa harga satuan; ketiadaan baris berarti warisan.
- Tema memakai token peran, bukan hex mentah di komponen.

Skema lengkap dan urutan milestone ada di `../replica/architecture.md`.
Peta layar dan alur ada di `../replica/recon.md`.

## Batasan saat ini

- WPF belum pernah dijalankan atau diuji visual di Windows. Build, XAML, dan
  referensi resource sudah divalidasi; font embedded, focus visual, dan
  `TabControl` kustom masih perlu dicek di mesin Windows.
- Keyboard layar dan tombol daya pada layar login belum dibuat.
- Pemulihan database dan penjadwalan cadangan kustom belum ada.
- Impor Aronium Lite belum memindahkan riwayat transaksi, pajak nominal tetap,
  diskon khusus pelanggan, dan foto produk. File `.bak` Aronium Pro belum dapat
  dibaca langsung.
- Layar M1–M8 masih berupa stub status.
- Resolusi target 1366×768; belum ada uji DPI atau layar sentuh.

## Lisensi aset

Font Inter dan Cascadia Mono memakai SIL Open Font License. Ikon Lucide
memakai ISC. Teks lisensi ada di `src/Pos.App/Assets/Fonts/`.
