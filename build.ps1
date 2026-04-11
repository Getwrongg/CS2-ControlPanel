$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'CS2AdminTool/CS2AdminTool.csproj'
$projectDir = Split-Path $projectPath -Parent
$projectBinPath = Join-Path $projectDir 'bin'
$projectObjPath = Join-Path $projectDir 'obj'
$distPath = Join-Path $PSScriptRoot 'dist'
$publishPath = Join-Path $distPath 'publish'
$zipPath = Join-Path $distPath 'CS2AdminTool.zip'
$ridOutputPath = Join-Path $projectDir 'bin/Release/net8.0-windows/win-x64'
$ridRuntimeConfigPath = Join-Path $ridOutputPath 'CS2AdminTool.runtimeconfig.json'

Write-Host 'Cleaning previous builds...'
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
if (Test-Path $projectBinPath) {
    Remove-Item $projectBinPath -Recurse -Force
}
if (Test-Path $projectObjPath) {
    Remove-Item $projectObjPath -Recurse -Force
}
if (-not (Test-Path $distPath)) {
    New-Item -Path $distPath -ItemType Directory | Out-Null
}

Write-Host 'Publishing application...'
dotnet clean $projectPath -c Release -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

dotnet build $projectPath -c Release -r win-x64 -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $ridRuntimeConfigPath)) {
    $candidatePaths = @(
        (Join-Path $projectDir 'obj/Release/net8.0-windows/win-x64/CS2AdminTool.runtimeconfig.json'),
        (Join-Path $projectDir 'obj/Release/net8.0-windows/CS2AdminTool.runtimeconfig.json'),
        (Join-Path $projectDir 'bin/Release/net8.0-windows/CS2AdminTool.runtimeconfig.json')
    )

    $sourceRuntimeConfig = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($sourceRuntimeConfig) {
        if (-not (Test-Path $ridOutputPath)) {
            New-Item -Path $ridOutputPath -ItemType Directory | Out-Null
        }

        Copy-Item $sourceRuntimeConfig $ridRuntimeConfigPath -Force
        Write-Host "Recovered missing runtimeconfig from: $sourceRuntimeConfig"
    }
    else {
        throw "Missing runtimeconfig.json after build. Checked: $($candidatePaths -join ', ')"
    }
}

dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishPath -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host 'Creating zip archive...'
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

Write-Host "Build complete. Artifact: $zipPath"
