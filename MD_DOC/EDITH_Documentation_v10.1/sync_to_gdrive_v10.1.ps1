<#
.SYNOPSIS
    EDITH Platform - Google Drive Synchronization Utility (v10.1)
.DESCRIPTION
    Packages all organized documentation in F:\EDITH\MD_DOC under unified Version 10.1
    and facilitates synchronization with the target Google Drive folder:
    https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link
#>

$TargetFolderUrl = "https://drive.google.com/drive/folders/1teuXH8b2hZKMboLJTVud_Rl0v4qlx8YW?usp=drive_link"
$DocRoot = "F:\EDITH\MD_DOC"
$ZipPackage = "$DocRoot\EDITH_Documentation_Library_v10.1.zip"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  EDITH AML Platform - Google Drive Sync Utility (v10.1)  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Compress the organized documentation
Write-Host "`n[1/3] Refreshing documentation bundle: $ZipPackage..." -ForegroundColor Yellow
$FoldersToZip = @(
    "$DocRoot\01_Executive_and_Pitch",
    "$DocRoot\02_Product_and_Requirements",
    "$DocRoot\03_Technical_Architecture_and_Database",
    "$DocRoot\04_Operations_and_Deployment",
    "$DocRoot\README.md"
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
    $Dest = "$FoundDrive\EDITH_MD_DOC"
    Copy-Item -Path $DocRoot -Destination $Dest -Recurse -Force
    Write-Host "  ✓ Synchronized to $Dest successfully!" -ForegroundColor Green
} else {
    Write-Host "  • Google Drive Desktop client is not mounted locally." -ForegroundColor Gray
    Write-Host "  • Target Web Folder: $TargetFolderUrl" -ForegroundColor Cyan
}

# 3. Open Target Google Drive folder in browser for seamless upload
Write-Host "`n[3/3] Opening Google Drive web upload target..." -ForegroundColor Yellow
$ChromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe"
if (Test-Path $ChromePath) {
    Start-Process -FilePath $ChromePath -ArgumentList "$TargetFolderUrl"
} else {
    Start-Process "$TargetFolderUrl"
}

Write-Host "`n✓ Sync process initialized. Simply drag '$ZipPackage' or the organized folders into the opened browser window." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
