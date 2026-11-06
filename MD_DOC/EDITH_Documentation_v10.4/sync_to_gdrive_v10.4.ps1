<#
.SYNOPSIS
    EDITH Platform - Google Drive Synchronization Utility (v10.4)
.DESCRIPTION
    Packages all organized documentation in EDITH_Documentation_v10.4 under unified Version 10.4
    and facilitates synchronization with the target Google Drive folder:
    https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link
#>

$TargetFolderUrl = "https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link"
$DocRoot = $PSScriptRoot
$ZipPackage = "$DocRoot\EDITH_Documentation_Library_v10.4.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  EDITH AML Platform - Google Drive Sync Utility (v10.4)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Compress the organized documentation
Write-Host "`n[1/3] Refreshing documentation bundle: $ZipPackage..." -ForegroundColor Yellow
$FoldersToZip = @(
    "$DocRoot\01_Executive_and_Pitch",
    "$DocRoot\02_Product_and_Requirements",
    "$DocRoot\03_Technical_Architecture_and_Database",
    "$DocRoot\04_Operations_and_Deployment",
    "$DocRoot\README.md",
    "$DocRoot\README_v10.4.md"
)

if (Test-Path $ZipPackage) {
    Remove-Item $ZipPackage -Force
}

Compress-Archive -Path $FoldersToZip -DestinationPath $ZipPackage -CompressionLevel Optimal
$ZipItem = Get-Item $ZipPackage
Write-Host "  ✓ Package generated successfully! Size: $([math]::Round($ZipItem.Length / 1KB, 2)) KB" -ForegroundColor Green

# 2. Check for local Google Drive desktop integration
Write-Host "`n[2/3] Checking for local Google Drive desktop installation..." -ForegroundColor Yellow
$DriveRoots = @("G:\My Drive", "G:\Shared drives", "$HOME\Google Drive")
$FoundDrive = $null

foreach ($d in $DriveRoots) {
    if (Test-Path $d) {
        $FoundDrive = $d
        break
    }
}

if ($FoundDrive) {
    Write-Host "  ✓ Local Google Drive detected at: $FoundDrive" -ForegroundColor Green
    $Destination = "$FoundDrive\EDITH\Documentation\EDITH_Documentation_v10.4"
    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    Write-Host "  Synchronizing files directly to Google Drive folder..." -ForegroundColor Cyan
    Copy-Item -Path $ZipPackage -Destination "$Destination\EDITH_Documentation_Library_v10.4.zip" -Force
    Write-Host "  ✓ Synchronization completed directly via local Google Drive file stream!" -ForegroundColor Green
} else {
    Write-Host "  Google Drive desktop app not detected in standard locations." -ForegroundColor DarkYellow
    Write-Host "`n[3/3] Opening target Google Drive folder in default web browser..." -ForegroundColor Yellow
    Write-Host "  URL: $TargetFolderUrl" -ForegroundColor Cyan
    Start-Process $TargetFolderUrl
    Write-Host "  ✓ Upload the package to Google Drive by dragging and dropping: `"$ZipPackage`"" -ForegroundColor Green
}

Write-Host "`nDone." -ForegroundColor Cyan
