# OverTheAir

A copyable Windows template: Hello World WPF app, WiX installer, and a GitHub Action that publishes an over-the-air update. Nothing else.

Push a `vX.Y.Z` tag. The Action builds `OverTheAirSetup.exe`, attaches it to a GitHub Release, and the running app offers that release the next time it starts.

## Layout

| Path | Role |
|---|---|
| `OverTheAir/` | net8 WPF Hello World + in-app updater |
| `OverTheAir.Setup/` | WiX payload (`Package.wxs`) and Burn bundle (`Bundle.wxs`) |
| `scripts/` | Version parsing and installer build |
| `.github/workflows/release.yml` | Tag → build → WiX → GitHub Release |

## Local build

```powershell
dotnet build OverTheAir.sln -c Release
dotnet tool install --global wix --version 5.0.2
powershell -ExecutionPolicy Bypass -File scripts\Build-Installer.ps1 -Configuration Release -Version 0.0.1
```

Installers land in `OverTheAir.Setup\bin\`.

## First release

1. Create a GitHub repo and push this folder to it.
2. Set `LatestReleaseUrl` in `OverTheAir/Services/UpdateChecker.cs` to `https://api.github.com/repos/<owner>/<repo>/releases/latest`.
3. Tag and push:

```powershell
git tag v0.0.1
git push origin v0.0.1
```

The Action publishes `OverTheAirSetup.exe` and `OverTheAir.msi` on that tag. Later tags that are newer than the installed `AssemblyVersion` are offered in-app.

## Reuse in another application

Copy these pieces, then rename the product:

- `.github/workflows/release.yml`
- `scripts/Get-Version.ps1` and `scripts/Build-Installer.ps1`
- `OverTheAir.Setup/Package.wxs`, `Bundle.wxs`, and `Assets/License.rtf`
- `OverTheAir/Services/UpdateChecker.cs` and `UpdateReleaseParser.cs`, plus a startup call like the one in `MainWindow.xaml.cs`

Change:

- Product name, exe name, and harvest path (`BinDir`)
- Both WiX `UpgradeCode` GUIDs (MSI and bundle must stay different from each other)
- `LatestReleaseUrl` and the User-Agent in `UpdateChecker`
- Installer file names in `Build-Installer.ps1` and `release.yml`

WiX stays pinned at **5.0.2**. v6+ requires a paid EULA that GitHub Actions cannot accept interactively.
