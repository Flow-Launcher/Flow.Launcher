# Analyze plugin runtimes
Write-Host "=== Plugin Runtime Folders ===" -ForegroundColor Cyan

if (-not (Test-Path "Output/Release/Avalonia/Plugins")) {
    Write-Host "Output/Release/Avalonia/Plugins not found" -ForegroundColor Yellow
    return
}

$pluginRuntimes = Get-ChildItem "Output/Release/Avalonia/Plugins" -Recurse -Directory -Filter "runtimes"

$totalSize = 0
foreach ($runtimeDir in $pluginRuntimes) {
    $subdirs = Get-ChildItem $runtimeDir.FullName -Directory
    $size = (Get-ChildItem $runtimeDir.FullName -Recurse -File | Measure-Object Length -Sum).Sum
    $totalSize += $size
    
    $plugin = $runtimeDir.Parent.Name
    Write-Host ""
    Write-Host ("Plugin: {0}" -f $plugin) -ForegroundColor Yellow
    Write-Host ("  Path: {0}" -f $runtimeDir.FullName)
    Write-Host ("  Size: {0:N2} MB" -f ($size / 1MB))
    Write-Host ("  Platforms: {0}" -f ($subdirs.Name -join ", "))
}

Write-Host ""
Write-Host ("TOTAL Plugin Runtimes: {0:N2} MB" -f ($totalSize / 1MB)) -ForegroundColor Green
