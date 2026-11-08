# Zoo CLI Safe Windows Updater (Bypasses OS Error 5)
Write-Host "Updating Zoo CLI..." -ForegroundColor Cyan

$zooDir = "$env:LOCALAPPDATA\Zoo"
$zooExe = "$zooDir\zoo.exe"
$zooTmp = "$zooDir\zoo.new.exe"
$zooBak = "$zooDir\zoo.old.exe"

if (!(Test-Path $zooDir)) {
    New-Item -ItemType Directory -Path $zooDir -Force | Out-Null
}

# Kill any running zoo processes
Get-Process -Name zoo -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$downloadUrl = "https://github.com/KittyCAD/cli/releases/download/v0.2.204/zoo-x86_64-pc-windows-gnu"

if (Test-Path $zooTmp) { Remove-Item $zooTmp -Force -ErrorAction SilentlyContinue }

Write-Host "Downloading latest Zoo CLI binary from GitHub..." -ForegroundColor Cyan
if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
    & curl.exe -L -o $zooTmp $downloadUrl
} else {
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($downloadUrl, $zooTmp)
}

if (!(Test-Path $zooTmp)) {
    Write-Error "Download failed. Check your network connection."
    exit 1
}

# Safe replacement: rename locked file first, then move in new file
if (Test-Path $zooBak) { Remove-Item $zooBak -Force -ErrorAction SilentlyContinue }
if (Test-Path $zooExe) { Rename-Item $zooExe "zoo.old.exe" -Force }
Move-Item $zooTmp $zooExe -Force
if (Test-Path $zooBak) { Remove-Item $zooBak -Force -ErrorAction SilentlyContinue }

Write-Host "Zoo CLI updated successfully!" -ForegroundColor Green
& "$zooExe" version
