# POS Desktop

Aplikasi kasir Windows untuk toko sendiri. WPF native, database SQLite lokal,
tanpa server dan tanpa Chromium. Antarmuka berbahasa Indonesia dengan sapaan
formal “Anda”.

Status: **M0 — fondasi selesai**. Alur kasir inti (M1) belum dibangun; menu
yang belum tersedia membuka halaman status, bukan tautan mati.

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
- Shell aplikasi: bilah atas, navigasi Beranda / Manajemen / Pengaturan.
- Menu utama S13 dengan status setiap tujuan.
- Kerangka Manajemen S14 dan Pengaturan S27.
- Tema gelap (bawaan) dan terang, tersimpan per pengguna Windows.
- Cadangan harian `VACUUM INTO` saat start, retensi 30 file, tombol cadangan
  manual di Pengaturan.
- Audit append-only, pemeriksaan integritas SQLite saat start, single-instance
  mutex, dan log Serilog.

## Lokasi data

Semua data pengguna berada di profil Windows, bukan di folder instalasi:

```text
%LOCALAPPDATA%\PosDesktop\
  data\pos.db
  backups\pos-YYYYMMDD-HHmmss.db
  logs\pos-YYYYMMDD.log
  ui.json
```

`ui.json` hanya menyimpan pilihan tema sebelum login. Pengaturan toko ada di
tabel `settings` di SQLite.

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
- Layar M1–M8 masih berupa stub status.
- Resolusi target 1366×768; belum ada uji DPI atau layar sentuh.

## Lisensi aset

Font Inter dan Cascadia Mono memakai SIL Open Font License. Ikon Lucide
memakai ISC. Teks lisensi ada di `src/Pos.App/Assets/Fonts/`.
