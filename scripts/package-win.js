'use strict';

/*
 * Build a Windows x64 Aroklon distribution without electron-builder.
 * Extracts the official Electron runtime, packs our app into app.asar,
 * renames the executable, stamps icon/version info and zips the result.
 *
 * Usage: node scripts/package-win.js
 */

const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const { execFileSync } = require('child_process');
const asar = require('@electron/asar');
const PELibrary = require('pe-library');
const ResEdit = require('resedit');

const ROOT = path.join(__dirname, '..');
const DIST = path.join(ROOT, 'dist');
const APP_NAME = 'Aroklon';
const VERSION = '1.0.3';
const ELECTRON_VERSION = '45.3.4';

function findElectronZip() {
  const cache = path.join(os.homedir(), '.cache', 'electron');
  const dirs = fs.readdirSync(cache);
  for (const d of dirs) {
    const p = path.join(cache, d, `electron-v${ELECTRON_VERSION}-win32-x64.zip`);
    if (fs.existsSync(p)) return p;
  }
  throw new Error('Electron win32-x64 zip not found in cache');
}

function stageApp() {
  const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'aroklon-app-'));
  fs.cpSync(path.join(ROOT, 'src'), path.join(stage, 'src'), { recursive: true });
  // sql.js only needs these three files at runtime. The full package is ~30 MB;
  // trimming it keeps app.asar around 1 MB so updates stay tiny.
  const sqlSrc = path.join(ROOT, 'node_modules', 'sql.js');
  const sqlDst = path.join(stage, 'node_modules', 'sql.js');
  fs.mkdirSync(path.join(sqlDst, 'dist'), { recursive: true });
  fs.copyFileSync(path.join(sqlSrc, 'package.json'), path.join(sqlDst, 'package.json'));
  fs.copyFileSync(path.join(sqlSrc, 'dist', 'sql-wasm.js'), path.join(sqlDst, 'dist', 'sql-wasm.js'));
  fs.copyFileSync(path.join(sqlSrc, 'dist', 'sql-wasm.wasm'), path.join(sqlDst, 'dist', 'sql-wasm.wasm'));
  const pkg = JSON.parse(fs.readFileSync(path.join(ROOT, 'package.json'), 'utf8'));
  delete pkg.devDependencies;
  delete pkg.scripts;
  pkg.version = VERSION;
  fs.writeFileSync(path.join(stage, 'package.json'), JSON.stringify(pkg, null, 2));
  return stage;
}

function stampExe(exePath) {
  const data = fs.readFileSync(exePath);
  const exe = PELibrary.NtExecutable.from(data);
  const res = PELibrary.NtExecutableResource.from(exe);

  const groups = ResEdit.Resource.IconGroupEntry.fromEntries(res.entries);
  const iconFile = ResEdit.Data.IconFile.from(fs.readFileSync(path.join(ROOT, 'build', 'icon.ico')));
  if (groups.length) {
    ResEdit.Resource.IconGroupEntry.replaceIconsForResource(
      res.entries,
      groups[0].id,
      1033,
      iconFile.icons.map((item) => item.data)
    );
  }

  const viList = ResEdit.Resource.VersionInfo.fromEntries(res.entries);
  if (viList.length) {
    const vi = viList[0];
    vi.setFileVersion(1, 0, 0, 0, 1033);
    vi.setProductVersion(1, 0, 0, 0, 1033);
    vi.setStringValues(
      { lang: 1033, codepage: 1200 },
      {
        FileDescription: 'Aroklon Point of Sale',
        ProductName: 'Aroklon',
        CompanyName: 'Aroklon',
        LegalCopyright: 'Copyright (C) 2026 Aroklon',
        OriginalFilename: 'Aroklon.exe',
        InternalName: 'Aroklon',
      }
    );
    vi.outputToResourceEntries(res.entries);
  }

  res.outputResource(exe);
  fs.writeFileSync(exePath, Buffer.from(exe.generate()));
}

function extractZip(zip, dest) {
  if (process.platform === 'win32') {
    execFileSync('powershell', [
      '-NoProfile',
      '-Command',
      `Expand-Archive -Force -LiteralPath '${zip}' -DestinationPath '${dest}'`,
    ]);
  } else {
    execFileSync('unzip', ['-q', zip, '-d', dest]);
  }
}

function makeZip(zipPath, folder, cwd) {
  if (process.platform === 'win32') {
    execFileSync('powershell', [
      '-NoProfile',
      '-Command',
      `Compress-Archive -Force -Path '${path.join(cwd, folder)}' -DestinationPath '${zipPath}'`,
    ]);
  } else {
    execFileSync('zip', ['-qry', zipPath, folder], { cwd });
  }
}

(async () => {
  fs.mkdirSync(DIST, { recursive: true });
  const outDir = path.join(DIST, `${APP_NAME}-win32-x64`);
  if (fs.existsSync(outDir)) fs.rmSync(outDir, { recursive: true, force: true });
  fs.mkdirSync(outDir, { recursive: true });

  const zip = findElectronZip();
  console.log('Electron runtime:', zip);
  extractZip(zip, outDir);

  // Slim the base: keep only the en-US locale and drop the Chromium license page.
  const localesDir = path.join(outDir, 'locales');
  if (fs.existsSync(localesDir)) {
    for (const f of fs.readdirSync(localesDir)) {
      if (f.endsWith('.pak') && f !== 'en-US.pak') fs.rmSync(path.join(localesDir, f));
    }
  }
  const licensePage = path.join(outDir, 'LICENSES.chromium.html');
  if (fs.existsSync(licensePage)) fs.rmSync(licensePage);

  // Electron loads resources/app.asar when default_app.asar is absent.
  const defaultApp = path.join(outDir, 'resources', 'default_app.asar');
  if (fs.existsSync(defaultApp)) fs.rmSync(defaultApp);

  const stage = stageApp();
  console.log('Packing app.asar…');
  await asar.createPackage(stage, path.join(outDir, 'resources', 'app.asar'));
  fs.rmSync(stage, { recursive: true, force: true });

  // Small app-only patch + manifest for the in-app updater.
  const appAsar = path.join(outDir, 'resources', 'app.asar');
  const releaseDir = path.join(DIST, 'releases');
  fs.mkdirSync(releaseDir, { recursive: true });
  const patchName = `app-${VERSION}.asar`;
  const patchPath = path.join(releaseDir, patchName);
  fs.copyFileSync(appAsar, patchPath);
  const sha256 = crypto.createHash('sha256').update(fs.readFileSync(patchPath)).digest('hex');
  let electronMajor = '45';
  try {
    electronMajor = JSON.parse(fs.readFileSync(path.join(ROOT, 'node_modules', 'electron', 'package.json'), 'utf8')).version.split('.')[0];
  } catch (e) {}
  const manifest = {
    version: VERSION,
    tag: `v${VERSION}`,
    asar: patchName,
    sha256,
    electron: electronMajor,
    minVersion: '1.0.0',
    built: new Date().toISOString(),
    notes: 'Aroklon app-only patch. Replace resources/app.asar; no runtime download needed.',
  };
  fs.writeFileSync(path.join(releaseDir, 'latest.json'), JSON.stringify(manifest, null, 2));
  console.log('patch:', patchPath, `${(fs.statSync(patchPath).size / 1024).toFixed(1)} KB`);
  console.log('manifest:', path.join(releaseDir, 'latest.json'));

  const exePath = path.join(outDir, 'electron.exe');
  const finalExe = path.join(outDir, `${APP_NAME}.exe`);
  fs.renameSync(exePath, finalExe);

  // Runtime window/taskbar icon (the exe file icon stays the Electron default
  // unless STAMP=1 is set and enough memory is available for resedit).
  fs.copyFileSync(path.join(ROOT, 'build', 'icon.png'), path.join(outDir, 'resources', 'aroklon-icon.png'));

  if (process.env.STAMP === '1') {
    console.log('Stamping icon and version…');
    try {
      stampExe(finalExe);
      console.log('Stamped', finalExe);
    } catch (e) {
      console.log('Stamp failed (exe still usable):', e.message);
    }
  } else {
    console.log('Skipped exe stamping (set STAMP=1 to enable).');
  }

  // Friendly readme next to the exe
  fs.writeFileSync(
    path.join(outDir, 'Baca dulu.txt'),
    `Aroklon ${VERSION} - Point of Sale untuk Windows\n\n` +
      `Jalankan Aroklon.exe. Tidak perlu instalasi.\n\n` +
      `Login pertama kali:\n` +
      `  Owner   - PIN 1234\n` +
      `  Cashier - PIN 1111\n\n` +
      `Sebelum jualan pertama: klik menu Register di kiri atas\n` +
      `(atau chip Register di bar atas), isi uang awal (opening cash),\n` +
      `lalu klik Open register.\n\n` +
      `Data disimpan otomatis di folder pengguna Windows (SQLite).\n` +
      `Backup tersedia di menu Settings > Data.\n\n` +
      `Update aplikasi: Settings > Data > Check for updates.\n` +
      `Kalau update gagal, tutup Aroklon lalu jalankan Update-Aroklon.bat.\n` +
      `Update hanya mengganti app.asar (kecil), bukan runtime penuh.\n\n` +
      `Dibuat dengan Electron. Tidak berafiliasi dengan produk POS lain.\n`
  );

  // Standalone updater (fallback when the in-app updater cannot run)
  const updaterPs1 = `$ErrorActionPreference = 'Stop'
$repo = 'clasmil00a/aroklon'
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
  $headers = @{ 'User-Agent' = 'Aroklon-Updater'; 'Accept' = 'application/vnd.github+json' }
  $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers $headers
  $metaAsset = $release.assets | Where-Object { $_.name -eq 'latest.json' } | Select-Object -First 1
  if (-not $metaAsset) { throw 'latest.json tidak ditemukan' }
  $meta = Invoke-RestMethod $metaAsset.browser_download_url -Headers $headers
  $asarAsset = $release.assets | Where-Object { $_.name -eq $meta.asar } | Select-Object -First 1
  if (-not $asarAsset) { throw "aset $($meta.asar) tidak ditemukan" }
  $tmp = Join-Path $env:TEMP $meta.asar
  Invoke-WebRequest $asarAsset.browser_download_url -OutFile $tmp -UseBasicParsing -Headers $headers
  $hash = (Get-FileHash $tmp -Algorithm SHA256).Hash.ToLower()
  if ($hash -ne $meta.sha256) { throw 'SHA-256 tidak cocok. Update dibatalkan.' }
  taskkill /IM Aroklon.exe /F 2>$null | Out-Null
  Start-Sleep -Seconds 2
  Copy-Item $tmp (Join-Path $appDir 'resources\\app.asar') -Force
  Remove-Item $tmp -Force
  Start-Process (Join-Path $appDir 'Aroklon.exe')
  Write-Host "Update v$($meta.version) berhasil. Aroklon dibuka ulang."
} catch {
  Write-Host "Update gagal: $($_.Exception.Message)"
  Read-Host 'Tekan Enter untuk keluar'
}
`;
  fs.writeFileSync(path.join(outDir, 'Update-Aroklon.ps1'), updaterPs1);
  fs.writeFileSync(
    path.join(outDir, 'Update-Aroklon.bat'),
    `@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-Aroklon.ps1"\r\npause\r\n`
  );

  const zipPath = path.join(DIST, `Aroklon-win32-x64.zip`);
  if (fs.existsSync(zipPath)) fs.rmSync(zipPath);
  console.log('Zipping…');
  makeZip(zipPath, path.basename(outDir), DIST);
  console.log('DONE:', outDir);
  console.log('ZIP :', zipPath);
})().catch((e) => {
  console.error(e);
  process.exit(1);
});