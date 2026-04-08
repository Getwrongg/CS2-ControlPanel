$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'CS2AdminTool/CS2AdminTool.csproj'
$projectDir = Split-Path $projectPath -Parent
$projectBinPath = Join-Path $projectDir 'bin'
$projectObjPath = Join-Path $projectDir 'obj'
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
dotnet clean $projectPath -c Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host 'Creating zip archive...'
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

Write-Host "Build complete. Artifact: $zipPath"
