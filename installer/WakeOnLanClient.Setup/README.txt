WakeOnLanClient.Setup (WiX 5 MSI)
================================

This .wixproj uses Sdk="WixToolset.Sdk" and is intentionally NOT included in
WakeOnLanClient.sln, because Visual Studio 2022 cannot open WiX 5 projects unless
the HeatWave extension is installed.

Build MSI (recommended):
  From repo root:
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release

  Output:
    installer\WakeOnLanClient.Setup\bin\x86\Release\WakeOnLanClient.msi

Optional (edit .wixproj inside VS):
  Install "HeatWave for VS2022" from Visual Studio Marketplace, then add
  WakeOnLanClient.Setup.wixproj back to the solution if desired.
