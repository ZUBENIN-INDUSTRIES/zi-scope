<#
.SYNOPSIS
  Собирает готовую папку для переноса на компьютер с микроскопом:
  dist\LumeneraViewer\  =  LumeneraViewer.exe (self-contained, один файл)
                           Drivers\<установщик INFINITY ANALYZE/CAPTURE>.exe
                           README.txt
.EXAMPLE
  .\publish.ps1
  .\publish.ps1 -Output D:\usb\LumeneraViewer
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot 'dist\LumeneraViewer'),
    [string]$Configuration = 'Release'
)

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
if (-not $installer) {
    Fail "В папке installer\ нет установщика драйверов (infinity*.exe).`n       Положите туда infinity_analyze_and_capture_for_windows_v_*.exe и запустите скрипт снова."
}
if (-not (Test-Path $readme)) { Fail "Нет файла $readme" }
Write-Host "    Установщик драйверов: $($installer.Name) ($([math]::Round($installer.Length / 1MB, 1)) МБ)"

# 2. Сборка
Step "Собираю LumeneraViewer.exe ($Configuration, win-x64, self-contained, один файл)"
if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
$publishArgs = @(
    'publish', $project, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none', '-p:EnableCompressionInSingleFile=true', '-p:GenerateDocumentationFile=false',
    '-o', $Output, '--nologo', '-v:minimal'
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish завершился с кодом $LASTEXITCODE" }

# 3. Драйверы и README (csproj уже кладёт установщик в Drivers\, но проверяем и докладываем сами)
Step 'Кладу драйверы и README'
$drivers = Join-Path $Output 'Drivers'
New-Item -ItemType Directory -Force $drivers | Out-Null
Copy-Item $installer.FullName $drivers -Force
Copy-Item $readme (Join-Path $Output 'README.txt') -Force

# 4. Итог
$exe = Join-Path $Output 'LumeneraViewer.exe'
if (-not (Test-Path $exe)) { Fail "После сборки нет $exe" }
Step 'Готово'
Get-ChildItem $Output -Recurse -File | ForEach-Object {
    '    {0,-70} {1,8:N1} МБ' -f $_.FullName.Substring($Output.Length + 1), ($_.Length / 1MB)
}
Write-Host ''
Write-Host "Папка для переноса: $Output" -ForegroundColor Green
Write-Host 'Скопируйте её целиком на компьютер с микроскопом и запустите LumeneraViewer.exe.'
