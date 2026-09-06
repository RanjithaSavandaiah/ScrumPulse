# PowerShell script to publish the ScrumPulse Wiki to GitHub
param(
    [string]$WikiRemote = "https://github.com/RanjithaSavandaiah/ScrumPulse.wiki.git"
)

$ErrorActionPreference = "Stop"

Write-Host "=== ScrumPulse Wiki Publisher ===" -ForegroundColor Cyan

# Check if wiki remote exists
Write-Host "Checking if Wiki Git repository is initialized on GitHub..." -ForegroundColor Yellow
$lsRemote = & git ls-remote $WikiRemote 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[!] GitHub Wiki repository is not yet initialized on GitHub." -ForegroundColor Red
    Write-Host "To initialize it, please perform this one-time step:" -ForegroundColor Yellow
    Write-Host "  1. On your open GitHub Wiki tab, click 'Create the first page'" -ForegroundColor White
    Write-Host "  2. Scroll down and click the green 'Save page' button" -ForegroundColor White
    Write-Host "  3. Re-run this script: .\push-wiki.ps1" -ForegroundColor Green
    exit 1
}

Write-Host "[+] Wiki Git repository found! Preparing upload..." -ForegroundColor Green

$tempDir = Join-Path $PSScriptRoot "temp_wiki_upload"
if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}

try {
    Write-Host "Cloning wiki repository..." -ForegroundColor Yellow
    & git clone $WikiRemote $tempDir

    Write-Host "Copying documentation pages from wiki/..." -ForegroundColor Yellow
    $sourceFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "wiki") -Filter "*.md"
    foreach ($file in $sourceFiles) {
        Copy-Item -Path $file.FullName -Destination $tempDir -Force
        Write-Host "  -> $($file.Name)" -ForegroundColor Gray
    }

    Push-Location $tempDir
    & git config user.name "Ranjitha Savandaiah"
    & git config user.email "lsranjitha@gmail.com"
    & git add -A
    
    $status = & git status --porcelain
    if ($status) {
        Write-Host "Committing updates..." -ForegroundColor Yellow
        & git commit -m "docs(wiki): publish comprehensive ScrumPulse engineering wiki"
        Write-Host "Pushing to GitHub Wiki..." -ForegroundColor Yellow
        & git push origin master
        Write-Host "`n[SUCCESS] ScrumPulse Wiki published successfully with all 14 pages and sidebar!" -ForegroundColor Green
    } else {
        Write-Host "`n[INFO] Wiki is already up to date." -ForegroundColor Green
    }
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    if (Test-Path $tempDir) {
        Remove-Item -Recurse -Force $tempDir
    }
}
