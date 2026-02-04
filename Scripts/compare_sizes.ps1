# Size comparison script
Write-Host "=== WPF Debug (excluding Avalonia) ===" -ForegroundColor Cyan
$wpfRoot = Get-ChildItem "Output/Debug" -File | Measure-Object Length -Sum
Write-Host ("Root files: {0:N2} MB" -f ($wpfRoot.Sum / 1MB))

$wpfPlugins = Get-ChildItem "Output/Debug/Plugins" -Recurse -File | Measure-Object Length -Sum
Write-Host ("Plugins folder: {0:N2} MB" -f ($wpfPlugins.Sum / 1MB))

Write-Host ""
Write-Host "=== Avalonia Debug ===" -ForegroundColor Cyan
$avRoot = Get-ChildItem "Output/Debug/Avalonia" -File | Measure-Object Length -Sum
Write-Host ("Root files: {0:N2} MB" -f ($avRoot.Sum / 1MB))

if (Test-Path "Output/Debug/Avalonia/Plugins") {
    $avPlugins = Get-ChildItem "Output/Debug/Avalonia/Plugins" -Recurse -File | Measure-Object Length -Sum
    Write-Host ("Plugins folder: {0:N2} MB" -f ($avPlugins.Sum / 1MB))
}

$avRuntimes = Get-ChildItem "Output/Debug/Avalonia/runtimes" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum
Write-Host ("runtimes folder: {0:N2} MB" -f ($avRuntimes.Sum / 1MB))

Write-Host ""
Write-Host "=== Difference breakdown ===" -ForegroundColor Yellow
$wpfTotal = (Get-ChildItem "Output/Debug" -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\Avalonia\\" } | Measure-Object Length -Sum).Sum
$avTotal = (Get-ChildItem "Output/Debug/Avalonia" -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("WPF total (excl. Avalonia subfolder): {0:N2} MB" -f ($wpfTotal / 1MB))
Write-Host ("Avalonia total: {0:N2} MB" -f ($avTotal / 1MB))
Write-Host ("Difference: {0:N2} MB" -f (($avTotal - $wpfTotal + $wpfPlugins.Sum) / 1MB))
