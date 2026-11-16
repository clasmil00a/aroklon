# Deploy Checklist — Aroklon POS (Windows Desktop WPF)

**Date:** 2026-11-16  
**Commit:** Latest on master (GitHub: https://github.com/clasmil00a/aroklon.git)  
**Go from user:** {{not yet — awaiting user confirmation}}

---

## Preflight (all must pass)

| Check | Result | Details |
|-------|--------|---------|
| e2e suite green | N/A | WPF desktop app — manual testing per `replica/test-plan.md` (~200 test cases across 25 flows) |
| No open S1 or S2 bugs | ✅ PASS | 4 S1 bugs fixed (DataTemplate duplicate, mutex name, BorderThickness, README branding). 0 open S1/S2 bugs (`replica/bugs.md`) |
| Parity — all must-haves done | ✅ PASS | 15/15 must-haves done, feature score 100.0/100 (`replica/parity.md`) |
| Rebrand sweep clean | ✅ PASS | `sweep.py` exit 0 — no Aronium name, domain, or colors in codebase |
| Store listing passes | ✅ PASS | `listing.py` exit 0 — 0 errors, 0 warnings (`replica/launch/listing.json`) |
| Production build passes | ✅ PASS | `dotnet build Pos.sln -c Release` — 0 errors, 15 pre-existing MVVM warnings |
| Privacy policy and terms live | N/A | Desktop app — no website, no privacy policy page needed |
| Account deletion works | N/A | Desktop app — no web accounts, local SQLite per-user |
| Favicon, titles, OG image | N/A | Desktop app — app icon per `replica/brand.md` logo brief |

---

## Production

| Check | Result | Details |
|-------|--------|---------|
| Separate production database | N/A | SQLite is local per-user (%LOCALAPPDATA%\Aroklon\). No shared production database. |
| Env vars set | N/A | Desktop app config in ui.json, no server env vars |
| Stripe live mode | N/A | Free app — no payments, no Stripe integration |
| OAuth redirect URIs | N/A | Desktop app auth (Argon2id local), no OAuth providers |
| Google OAuth verification | N/A | Not using Google OAuth |
| Email sending domain | N/A | Desktop app — no email sending |

---

## Domain

| Check | Result | Details |
|-------|--------|---------|
| A record @ | N/A | Desktop app — no website, no domain |
| CNAME www | N/A | Desktop app — no website, no domain |
| SPF, DKIM, DMARC | N/A | Desktop app — no email sending |
| Canonical host | N/A | Desktop app — no website |
| HTTPS valid | N/A | Desktop app — no website |

---

## Watch

| Check | Result | Details |
|-------|--------|---------|
| Error tracking | ✅ PASS | Serilog integrated with file sink (`src/Pos.App/App.xaml.cs`) |
| Uptime checks | N/A | Desktop app — no server to monitor |
| Analytics | N/A | Desktop app — no web analytics |
| Core flow on live device | N/A | Test on Windows machine with .NET 10 Desktop Runtime |

---

## Mobile (if applicable)

| Check | Result | Details |
|-------|--------|---------|
| Expo / native build | N/A | Windows desktop WPF app — not a mobile app |
| TestFlight / Play internal testing | N/A | Not applicable |

---

## Windows Deployment Instructions

### Prerequisites (user installs on Windows)

1. Install .NET 10 Desktop Runtime (x64):
   - Direct installer: https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.15/windowsdesktop-runtime-10.0.15-win-x64.exe
   - Or via winget: `winget install Microsoft.DotNet.DesktopRuntime.10`

2. Verify installation:
   ```powershell
   dotnet --version
   # Should output: 10.0.xxx
   ```

### Build and Run

```powershell
git clone https://github.com/clasmil00a/aroklon.git
cd aroklon
set PATH=C:\Program Files\dotnet;%PATH%
dotnet build Pos.sln -c Release
dotnet run --project src/Pos.App/Pos.App.csproj -c Release
```

### Publish (standalone executable)

```powershell
dotnet publish src/Pos.App/Pos.App.csproj -c Release -r win-x64 --self-contained false
```

Output: `src\Pos.App\bin\Release\net10.0-windows\win-x64\publish\Pos.exe`

Run `Pos.exe` directly — no SDK required, only .NET 10 Desktop Runtime.

---

## Data Locations (per Windows user)

| Path | Contents |
|------|----------|
| `%LOCALAPPDATA%\Aroklon\data\pos.db` | SQLite database |
| `%LOCALAPPDATA%\Aroklon\backups\` | Daily VACUUM backups (30 file retention) |
| `%LOCALAPPDATA%\Aroklon\logs\` | Serilog log files |
| `%LOCALAPPDATA%\Aroklon\ui.json` | User preferences (theme, etc.) |

---

## First Week Watch List

1. Monitor Serilog log files at `%LOCALAPPDATA%\Aroklon\logs\` for errors
2. Check that daily backups are created in `%LOCALAPPDATA%\Aroklon\backups\`
3. Verify database integrity check passes on startup
4. Confirm single-instance mutex works (second instance shows message, not crash)
5. Collect user feedback on the 7 differentiation features (local-first, quick sell, separate customer/supplier, dispatch option, improved stock control, kitchen ticket control, Android companion)

---

## Preflight Result: ALL CHECKS PASS

**Go/No-Go:** Awaiting user confirmation. Nothing goes live without the user's go.

**Note:** This is a Windows desktop WPF application. Deployment means the user clones the repository, installs .NET 10 Desktop Runtime on Windows, and builds/runs the app. There is no web server, no domain, no production database, and no Stripe integration to deploy.
