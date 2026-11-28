# Contributing

See [Build from source](README.md#build-from-source) for the build requirements
and the standard test commands. Run the commands below from the repository root.

## Stage or deploy a local build

PowerShell 7 is required for staging and deployment. Copy
[`local.props.example`](local.props.example) to `local.props` and set
`NinaExecutable` to the path to your installed `NINA.exe` so deployment can
check compatibility.

This command stages a Debug build under `artifacts/stage/Debug`.

```powershell
dotnet msbuild src/UnphasedNina/UnphasedNina.csproj -t:StagePlugin
```

Close N.I.N.A. before running the command to build, stage and install the plugin.

```powershell
dotnet msbuild src/UnphasedNina/UnphasedNina.csproj -t:DeployPlugin
```

Reopen N.I.N.A. to load the plugin.

## Opt-in tests

The chart smoke test writes previews to `%TEMP%`.

```powershell
dotnet test tests/UnphasedNina.Tests/UnphasedNina.Tests.csproj --filter "FullyQualifiedName~PlannerChartRenderSmoke"
```

To compare overlay positions against an independent ephemeris, build the Release
test project.

```powershell
dotnet build tests/UnphasedNina.Tests/UnphasedNina.Tests.csproj -c Release
```

Copy the `External` folder from your N.I.N.A. installation into
`tests/UnphasedNina.Tests/bin/Release/net8.0-windows/` beside the test assembly.
Then run the comparison.

```powershell
dotnet test tests/UnphasedNina.Tests/UnphasedNina.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~OverlayAltitudesAgreeWithIndependentEphemerisAndFollowTheLatestFrame"
```

The target lookup test uses the installed N.I.N.A. catalogue at
`%LOCALAPPDATA%\NINA\NINA.sqlite`.

```powershell
dotnet test tests/UnphasedNina.Tests/UnphasedNina.Tests.csproj --filter "FullyQualifiedName~CatalogTypeRequiresAnExactAliasAndMatchingPosition"
```
