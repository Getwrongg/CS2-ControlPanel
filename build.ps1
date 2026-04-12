$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'CS2AdminTool/CS2AdminTool.csproj'
$projectDir = Split-Path $projectPath -Parent
$projectBinPath = Join-Path $projectDir 'bin'
$projectObjPath = Join-Path $projectDir 'obj'
$distPath = Join-Path $PSScriptRoot 'dist'
$publishPath = Join-Path $distPath 'publish'
$zipPath = Join-Path $distPath 'CS2AdminTool.zip'

function Remove-PathSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [switch]$Recurse
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        if ($Recurse) {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        }
        else {
            Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
        }
    }
    catch [System.IO.DirectoryNotFoundException], [System.IO.FileNotFoundException], [System.Management.Automation.ItemNotFoundException] {
        Write-Host "Skipping already-missing path while cleaning: $Path"
    }
    catch {
        Write-Warning "Initial delete failed for '$Path'. Retrying once. Error: $($_.Exception.Message)"
        Start-Sleep -Milliseconds 200

        if (Test-Path -LiteralPath $Path) {
            if ($Recurse) {
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
            }
            else {
                Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Write-Host 'Cleaning previous builds...'
Remove-PathSafe -Path $publishPath -Recurse
Remove-PathSafe -Path $zipPath
Remove-PathSafe -Path $projectBinPath -Recurse
Remove-PathSafe -Path $projectObjPath -Recurse
if (-not (Test-Path $distPath)) {
    New-Item -Path $distPath -ItemType Directory | Out-Null
}

Write-Host 'Publishing application...'
# IMPORTANT: Keep this publish model aligned with CS2AdminTool.csproj (RuntimeIdentifier=win-x64, self-contained publish).
dotnet clean $projectPath -c Release -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $publishPath -m:1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host 'Creating zip archive...'
Compress-Archive -Path "$publishPath/*" -DestinationPath $zipPath -Force

Write-Host "Build complete. Artifact: $zipPath"
