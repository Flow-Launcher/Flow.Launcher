# Duplicate DLL analysis
Write-Host "=== Duplicate DLLs in Avalonia Plugins ===" -ForegroundColor Cyan

$avRootDlls = Get-ChildItem "Output/Debug/Avalonia" -Filter "*.dll"
$avRootDllNames = $avRootDlls | Select-Object -ExpandProperty Name

$totalDuplicateSize = 0
$duplicatesByPlugin = @{}

Get-ChildItem "Output/Debug/Avalonia/Plugins" -Directory | ForEach-Object {
    $pluginName = $_.Name
    $pluginDuplicates = 0
    
    Get-ChildItem $_.FullName -Recurse -Filter "*.dll" | ForEach-Object {
        if ($avRootDllNames -contains $_.Name) {
            $pluginDuplicates += $_.Length
            $totalDuplicateSize += $_.Length
        }
    }
    
    if ($pluginDuplicates -gt 0) {
        $duplicatesByPlugin[$pluginName] = $pluginDuplicates
    }
}

Write-Host ""
Write-Host "Duplicates by plugin (MB):" -ForegroundColor Yellow
$duplicatesByPlugin.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    Write-Host ("{0,8:N2} MB  {1}" -f ($_.Value / 1MB), $_.Key)
}

Write-Host ""
Write-Host ("TOTAL DUPLICATE SIZE: {0:N2} MB" -f ($totalDuplicateSize / 1MB)) -ForegroundColor Green
Write-Host ""

# Also show the total current size vs potential reduced size
$currentSize = (Get-ChildItem "Output/Debug/Avalonia" -Recurse -File | Measure-Object Length -Sum).Sum
$reducedSize = $currentSize - $totalDuplicateSize
Write-Host ("Current Avalonia size: {0:N2} MB" -f ($currentSize / 1MB))
Write-Host ("After dedup: {0:N2} MB" -f ($reducedSize / 1MB))
