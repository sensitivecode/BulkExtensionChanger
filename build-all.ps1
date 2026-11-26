param(
    [string]$Version = "1.1.0"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
Write-Host "=== [1/2] Сборка Windows установщика (v$Version) ==="
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "build-setup.ps1") -Version $Version
if ($LASTEXITCODE -ne 0) { throw "Ошибка сборки Windows установщика" }

Write-Host "=== [2/2] Сборка пакетов для macOS и Linux (v$Version) ==="
python (Join-Path $root "Installer\pack_crossplatform.py") --version $Version
if ($LASTEXITCODE -ne 0) { throw "Ошибка сборки пакетов macOS/Linux" }

Write-Host "`nГотовые установщики в $root\dist:"
Get-ChildItem (Join-Path $root "dist") -File | Select-Object Name, Length
