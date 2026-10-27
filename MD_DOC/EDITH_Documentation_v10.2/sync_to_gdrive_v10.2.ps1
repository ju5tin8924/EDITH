<#
.SYNOPSIS
    EDITH Platform - Google Drive Synchronization Utility (v10.2)
.DESCRIPTION
    Packages all organized documentation in EDITH_Documentation_v10.2 under unified Version 10.2
    and facilitates synchronization with the target Google Drive folder:
    https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link
#>

$TargetFolderUrl = "https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link"
$DocRoot = $PSScriptRoot
$ZipPackage = "$DocRoot\EDITH_Documentation_Library_v10.2.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  EDITH AML Platform - Google Drive Sync Utility (v10.2)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Compress the organized documentation
Write-Host "`n[1/3] Refreshing documentation bundle: $ZipPackage..." -ForegroundColor Yellow
$FoldersToZip = @(
    "$DocRoot\01_Executive_and_Pitch",
    "$DocRoot\02_Product_and_Requirements",
    "$DocRoot\03_Technical_Architecture_and_Database",
    "$DocRoot\04_Operations_and_Deployment",
    "$DocRoot\README.md",
    "$DocRoot\README_v10.2.md"
)

if (Test-Path $ZipPackage) {
    Remove-Item $ZipPackage -Force
}

Compress-Archive -Path $FoldersToZip -DestinationPath $ZipPackage -CompressionLevel Optimal
$ZipItem = Get-Item $ZipPackage
Write-Host "  âœ“ Package generated successfully! Size: $([math]::Round($ZipItem.Length / 1KB, 2)) KB" -ForegroundColor Green

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
    Write-Host "  âœ“ Local Google Drive detected at: $FoundDrive" -ForegroundColor Green
    $Dest = "$FoundDrive\EDITH_Documentation_v10.2"
    if (-not (Test-Path $Dest)) {
        New-Item -ItemType Directory -Path $Dest -Force | Out-Null
    }
    Copy-Item -Path $ZipPackage -Destination $Dest -Force
    Copy-Item -Path "$DocRoot\*" -Destination $Dest -Recurse -Force
    Write-Host "  âœ“ Successfully copied entire documentation suite to: $Dest" -ForegroundColor Green
} else {
    Write-Host "  â„¹ Google Drive Desktop client not found in standard paths." -ForegroundColor Gray
}

# 3. Open browser to the target Google Drive folder
Write-Host "`n[3/3] Opening Google Drive target folder in browser..." -ForegroundColor Yellow
Start-Process $TargetFolderUrl

Write-Host "`n----------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Upload the generated zip package directly into Google Drive:" -ForegroundColor White
Write-Host "  ðŸ“¦ $ZipPackage" -ForegroundColor Green
Write-Host "----------------------------------------------------------`n" -ForegroundColor Cyan