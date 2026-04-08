$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'CS2AdminTool/CS2AdminTool.csproj'
$distPath = Join-Path $PSScriptRoot 'dist'
$publishPath = Join-Path $distPath 'publish'
$zipPath = Join-Path $distPath 'CS2AdminTool.zip'

Write-Host 'Cleaning previous builds...'
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
if (-not (Test-Path $distPath)) {
    New-Item -Path $distPath -ItemType Directory | Out-Null
}

Write-Host 'Publishing application...'
dotnet clean $projectPath -c Release -r win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

dotnet build $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:GenerateRuntimeConfigurationFiles=true `
    -p:UseAppHost=true `
    -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$runtimeConfigPath = Join-Path $PSScriptRoot 'CS2AdminTool/bin/Release/net8.0-windows/win-x64/CS2AdminTool.runtimeconfig.json'
if (-not (Test-Path $runtimeConfigPath)) {
    throw "Expected runtime config was not generated: $runtimeConfigPath"
}

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-build `
    -p:GenerateRuntimeConfigurationFiles=true `
    -p:UseAppHost=true `
    -p:PublishSingleFile=false `
    -o $publishPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $publishPath 'CS2AdminTool.exe'
if (-not (Test-Path $exePath)) {
    $publishedFiles = Get-ChildItem -Path $publishPath -File | Select-Object -ExpandProperty Name
    throw "Publish completed but expected executable was not found at $exePath. Files present: $($publishedFiles -join ', ')"
}

Write-Host 'Creating zip archive...'
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

Write-Host "Build complete. Artifact: $zipPath"
