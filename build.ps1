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
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishPath

Write-Host 'Creating zip archive...'
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

Write-Host "Build complete. Artifact: $zipPath"
