# E2E Tests — Aroklon POS (WPF Desktop)

**Important:** This is a **WPF desktop application**, not a web application. Playwright e2e tests are **not applicable** because Playwright only automates browsers.

## Automated Testing Options for WPF

| Tool | Description | Status |
|------|-------------|--------|
| **WinAppDriver** | Microsoft's Windows Application Driver for WPF/UWP automation | Not set up |
| **FlaUI** | .NET UI automation library for WPF | Not set up |
| **White** | UI automation framework for WPF | Not set up |
| **Manual testing** | Use `replica/test-plan.md` checklist | ✅ Recommended |

## Manual Testing

Run the test cases in `replica/test-plan.md` on a Windows machine with .NET 10 Desktop Runtime installed.

### Quick Start

```powershell
# Clone the repo
git clone https://github.com/clasmil00a/aroklon.git
cd aroklon

# Install .NET 10 Desktop Runtime on Windows first
# Then build
dotnet build Pos.sln -c Release

# Run
dotnet run --project src/Pos.App/Pos.App.csproj -c Release
```

### Test Checklist

Use `replica/test-plan.md` for all test cases:
- F01 Onboarding
- F02 Login
- F03 Cash Sale (core flow)
- F04 Touch Sale
- F05 Advanced Product Search
- F06 Sales History
- F07 Open Orders
- F08 Receipt View
- F09 Unit Master
- F10 Product List
- F11 Price Lists
- F12 Product Price
- F13 Customer Assignment
- F14 Cash Movement
- F15 Refund
- F16 End of Day
- F17 Product Groups
- F18 Product Editor
- F19 Stock Adjustment
- F20 Reports
- F21 Settings
- F22 User Management
- F23 Payment Types
- F24 Database Check
- F25 Home Navigation

## Bug Reporting

Report bugs in `replica/bugs.md` using the format provided there.

## Setting Up WinAppDriver (Optional)

If you want to automate WPF testing:

```powershell
# Install WinAppDriver on Windows
# Download from: https://github.com/microsoft/WinAppDriver

# Start WinAppDriver
WinAppDriver.exe

# Install Node.js bindings
npm i -D @mui/x-date-pickers  # or winappdriver npm package

# Write tests using Appium-compatible API
```

See `e2e.example.spec.ts` template for structure reference (adapted for WPF selectors).
