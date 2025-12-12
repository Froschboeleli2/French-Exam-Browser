# Script to create a portable package of SafeExamBrowser
# This packages all necessary files for distribution

$ErrorActionPreference = "Stop"

$sourceDir = "SafeExamBrowser.Runtime\bin\x64\Debug"
$outputZip = "SafeExamBrowser-Portable.zip"

Write-Host "Creating portable package..." -ForegroundColor Cyan

if (-not (Test-Path $sourceDir)) {
    Write-Host "ERROR: Build directory not found: $sourceDir" -ForegroundColor Red
    Write-Host "Please build the solution first!" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path "$sourceDir\SafeExamBrowser.exe")) {
    Write-Host "ERROR: SafeExamBrowser.exe not found!" -ForegroundColor Red
    Write-Host "Please build the solution first!" -ForegroundColor Yellow
    exit 1
}

# Check for vocabulary.txt
if (Test-Path "SafeExamBrowser.UserInterface.Desktop\vocabulary.txt") {
    Copy-Item "SafeExamBrowser.UserInterface.Desktop\vocabulary.txt" -Destination "$sourceDir\vocabulary.txt" -Force
    Write-Host "[OK] Copied vocabulary.txt" -ForegroundColor Green
} else {
    Write-Host "[!] vocabulary.txt not found (optional)" -ForegroundColor Yellow
}

# Remove old zip if exists
if (Test-Path $outputZip) {
    Remove-Item $outputZip -Force
    Write-Host "[OK] Removed old package" -ForegroundColor Green
}

# Create zip
Write-Host ""
Write-Host "Packaging files..." -ForegroundColor Cyan
Compress-Archive -Path "$sourceDir\*" -DestinationPath $outputZip -CompressionLevel Optimal

$zipSize = [math]::Round((Get-Item $outputZip).Length / 1MB, 2)
Write-Host ""
Write-Host "[OK] Package created: $outputZip ($zipSize MB)" -ForegroundColor Green

Write-Host ""
Write-Host "=== PACKAGE CONTENTS ===" -ForegroundColor Cyan
Write-Host "The package includes:" -ForegroundColor White
Write-Host "  - SafeExamBrowser.exe (main executable)" -ForegroundColor Gray
Write-Host "  - SafeExamBrowser.Client.exe" -ForegroundColor Gray
Write-Host "  - All required DLL files" -ForegroundColor Gray
Write-Host "  - CefSharp browser engine files" -ForegroundColor Gray
Write-Host "  - vocabulary.txt (if present)" -ForegroundColor Gray

Write-Host ""
Write-Host "=== SYSTEM REQUIREMENTS ===" -ForegroundColor Cyan
Write-Host "Your friend needs:" -ForegroundColor White
Write-Host "  - Windows 10 version 1803 or later" -ForegroundColor Gray
Write-Host "  - .NET Framework 4.8 Runtime" -ForegroundColor Gray
Write-Host "     Download: https://dotnet.microsoft.com/download/dotnet-framework/net48" -ForegroundColor DarkGray
Write-Host "  - Visual C++ 2015-2022 Redistributable (x64)" -ForegroundColor Gray
Write-Host "     Download: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist" -ForegroundColor DarkGray

Write-Host ""
Write-Host "=== INSTRUCTIONS ===" -ForegroundColor Cyan
Write-Host "1. Extract the ZIP file to any folder" -ForegroundColor White
Write-Host "2. Install .NET Framework 4.8 (if not already installed)" -ForegroundColor White
Write-Host "3. Install Visual C++ Redistributable (if not already installed)" -ForegroundColor White
Write-Host "4. Run SafeExamBrowser.exe" -ForegroundColor White
Write-Host ""
Write-Host "Done! The package is ready to share." -ForegroundColor Green
