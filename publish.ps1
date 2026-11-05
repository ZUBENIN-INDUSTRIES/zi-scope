<#
.SYNOPSIS
  Собирает готовую папку для переноса на компьютер с микроскопом:
  dist\ZI-scope\  =  ZI-scope.exe (self-contained, один файл; установщик драйверов
                                   INFINITY ANALYZE/CAPTURE вшит внутрь, -p:EmbedDrivers=true)
                     README.txt   (для работы не нужен — достаточно одного exe)
  С -NoDrivers — публичная сборка без установщика Lumenera (его нельзя выкладывать в открытый доступ):
  dist\ZI-scope-public\ZI-scope.exe; драйверы пользователь ставит сам с сайта Teledyne Lumenera.
.EXAMPLE
  .\publish.ps1
  .\publish.ps1 -Output D:\usb\ZI-scope
  .\publish.ps1 -NoDrivers
#>
param(
    [string]$Output,
    [string]$Configuration = 'Release',
    [switch]$NoDrivers
)
if (-not $Output) { $Output = Join-Path $PSScriptRoot $(if ($NoDrivers) { 'dist\ZI-scope-public' } else { 'dist\ZI-scope' }) }

$ErrorActionPreference = 'Stop'
$root      = $PSScriptRoot
$project   = Join-Path $root 'src\LumeneraViewer\LumeneraViewer.csproj'
$readme    = Join-Path $root 'installer\README-УСТАНОВКА.txt'

function Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host "ОШИБКА: $text" -ForegroundColor Red; exit 1 }

# 1. Проверки до сборки
Step 'Проверяю окружение'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail '.NET SDK не найден. Установите: winget install Microsoft.DotNet.SDK.8'
}
$installer = Get-ChildItem -Path (Join-Path $root 'installer') -Filter 'infinity*.exe' -File -ErrorAction SilentlyContinue |
    Sort-Object Name | Select-Object -First 1
if (-not $installer -and -not $NoDrivers) {
    Fail "В папке installer\ нет установщика драйверов (infinity*.exe).`n       Положите туда infinity_analyze_and_capture_for_windows_v_*.exe и запустите скрипт снова."
}
if (-not (Test-Path $readme)) { Fail "Нет файла $readme" }
if ($NoDrivers) { Write-Host '    Публичная сборка: без установщика драйверов' }
else { Write-Host "    Установщик драйверов: $($installer.Name) ($([math]::Round($installer.Length / 1MB, 1)) МБ)" }

# 2. Сборка
Step "Собираю ZI-scope.exe ($Configuration, win-x64, self-contained, один файл, $(if ($NoDrivers) { 'без драйверов' } else { 'драйверы внутри' }))"
if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
$publishArgs = @(
    'publish', $project, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none', '-p:EnableCompressionInSingleFile=true', '-p:GenerateDocumentationFile=false',
    "-p:EmbedDrivers=$(if ($NoDrivers) { 'false' } else { 'true' })",
    '-o', $Output, '--nologo', '-v:minimal'
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish завершился с кодом $LASTEXITCODE" }

# 3. README (установщик драйверов уже внутри exe)
Step 'Кладу README'
Copy-Item $readme (Join-Path $Output 'README.txt') -Force

# 4. Итог
$exe = Join-Path $Output 'ZI-scope.exe'
if (-not (Test-Path $exe)) { Fail "После сборки нет $exe" }
if (-not $NoDrivers -and (Get-Item $exe).Length -lt $installer.Length) { Fail "ZI-scope.exe меньше установщика драйверов — установщик не вшился" }
# Обычная (не вшитая) сборка копирует установщик в Drivers\ — в публичной папке его быть не должно
if ($NoDrivers) { Remove-Item -Recurse -Force (Join-Path $Output 'Drivers') -ErrorAction SilentlyContinue }
Step 'Готово'
Get-ChildItem $Output -Recurse -File | ForEach-Object {
    '    {0,-70} {1,8:N1} МБ' -f $_.FullName.Substring($Output.Length + 1), ($_.Length / 1MB)
}
Write-Host ''
Write-Host "Папка для переноса: $Output" -ForegroundColor Green
Write-Host 'На компьютер с микроскопом достаточно скопировать один ZI-scope.exe и запустить его.'
