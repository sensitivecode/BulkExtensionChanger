$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$appOut = Join-Path $root "dist\app"
$payloadZip = Join-Path $root "Installer\payload.zip"
$setupExe = Join-Path $root "dist\BulkExtensionChanger-Setup.exe"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "Не найден компилятор C# (.NET Framework 4.x): $csc"
}

Write-Host "[1/3] Публикация BulkExtensionChanger..."
if (Test-Path $appOut) { Remove-Item $appOut -Recurse -Force }
dotnet publish (Join-Path $root "BulkExtensionChanger.csproj") -c Release -o $appOut
if ($LASTEXITCODE -ne 0) {
    throw "Ошибка сборки BulkExtensionChanger (код $LASTEXITCODE)"
}

Write-Host "[2/3] Упаковка файлов в payload.zip..."
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($appOut, $payloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "[3/3] Сборка установщика BulkExtensionChanger-Setup.exe..."
& $csc `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /out:$setupExe `
    "/win32icon:$(Join-Path $root 'Assets\app.ico')" `
    "/resource:$payloadZip,BulkExtensionChanger.Setup.payload.zip" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    (Join-Path $root "Installer\Setup.cs")

if ($LASTEXITCODE -ne 0) {
    throw "Ошибка компиляции Setup.exe (код $LASTEXITCODE)"
}

Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue
Write-Host "Готово: $setupExe ($([Math]::Round((Get-Item $setupExe).Length / 1KB, 1)) KB)"
