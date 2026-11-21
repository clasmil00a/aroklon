'use strict';

/*
 * Package Aroklon into a Windows app folder (Aroklon.exe + resources).
 * Usage: node scripts/package.js [win32|linux|darwin] [x64|arm64|ia32]
 */

const path = require('path');
const fs = require('fs');
const { execFileSync } = require('child_process');
const packager = require('@electron/packager').packager;

const platform = process.argv[2] || 'win32';
const arch = process.argv[3] || 'x64';
const root = path.join(__dirname, '..');
const outDir = path.join(root, 'dist');

const opts = {
  dir: root,
  out: outDir,
  platform,
  arch,
  name: 'Aroklon',
  appVersion: JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8')).version,
  appCopyright: 'Copyright © 2026 Aroklon',
  icon: path.join(root, 'build', 'icon.ico'),
  prune: true,
  overwrite: true,
  asar: true,
  ignore: [
    /^\/replica(\/|$)/,
    /^\/scripts(\/|$)/,
    /^\/build(\/|$)/,
    /^\/dist(\/|$)/,
    /^\/shots(\/|$)/,
    /\.md$/,
    /^\/tmp(\/|$)/,
  ],
  win32metadata: {
    CompanyName: 'Aroklon',
    FileDescription: 'Aroklon Point of Sale',
    ProductName: 'Aroklon',
    LegalCopyright: 'Copyright © 2026 Aroklon',
    OriginalFilename: 'Aroklon.exe',
    InternalName: 'Aroklon',
  },
};

(async () => {
  fs.mkdirSync(outDir, { recursive: true });
  console.log(`Packaging Aroklon for ${platform}/${arch}…`);
  const paths = await packager(opts);
  for (const p of paths) {
    try {
      fs.copyFileSync(path.join(root, 'build', 'icon.png'), path.join(p, 'resources', 'aroklon-icon.png'));
    } catch (e) {
      console.log('icon copy skipped:', e.message);
    }
    console.log('built:', p);
    const size = execFileSync('du', ['-sh', p]).toString().trim();
    console.log('size:', size);
  }

  if (platform === 'win32') {
    const folder = paths[0];
    const zipPath = path.join(outDir, `Aroklon-${platform}-${arch}.zip`);
    console.log('creating', zipPath);
    execFileSync('zip', ['-qry', zipPath, path.basename(folder)], { cwd: outDir });
    console.log('zip:', zipPath);
  }
})().catch((err) => {
  console.error(err);
  process.exit(1);
});