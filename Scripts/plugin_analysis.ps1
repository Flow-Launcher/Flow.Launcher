# Plugin size analysis
Write-Host "=== Plugin folder breakdown ===" -ForegroundColor Cyan

# Get root DLLs for WPF and Avalonia
$wpfRootDlls = Get-ChildItem "Output/Debug" -Filter "*.dll" | Select-Object -ExpandProperty Name
$avRootDlls = Get-ChildItem "Output/Debug/Avalonia" -Filter "*.dll" | Select-Object -ExpandProperty Name

Write-Host "WPF root DLLs: $($wpfRootDlls.Count)"
Write-Host "Avalonia root DLLs: $($avRootDlls.Count)"
Write-Host ""

# Calculate potential savings from duplicate removal in Avalonia plugins
if (Test-Path "Output/Debug/Avalonia/Plugins") {
    $duplicateSize = 0
    $pluginDlls = Get-ChildItem "Output/Debug/Avalonia/Plugins" -Recurse -Filter "*.dll"
    foreach ($dll in $pluginDlls) {
        if ($avRootDlls -contains $dll.Name) {
            $duplicateSize += $dll.Length
        }
    }
    Write-Host "Potential duplicate DLLs in Avalonia plugins: {0:N2} MB" -f ($duplicateSize / 1MB)
}

# Show each plugin size in Avalonia
Write-Host ""
Write-Host "=== Avalonia plugin sizes ===" -ForegroundColor Yellow
Get-ChildItem "Output/Debug/Avalonia/Plugins" -Directory | ForEach-Object {
    $size = (Get-ChildItem $_.FullName -Recurse -File | Measure-Object Length -Sum).Sum
    [PSCustomObject]@{ Name = $_.Name; SizeMB = [math]::Round($size / 1MB, 2) }
} | Sort-Object SizeMB -Descending | Format-Table -AutoSize

# Show extra DLLs in Avalonia root vs WPF root  
Write-Host ""
Write-Host "=== Avalonia-only root DLLs (not in WPF) ===" -ForegroundColor Yellow
$avOnlyDlls = Get-ChildItem "Output/Debug/Avalonia" -Filter "*.dll" | Where-Object { $wpfRootDlls -notcontains $_.Name }
foreach ($dll in ($avOnlyDlls | Sort-Object Length -Descending | Select-Object -First 15)) {
    Write-Host ("{0,8:N2} MB  {1}" -f ($dll.Length / 1MB), $dll.Name)
}
