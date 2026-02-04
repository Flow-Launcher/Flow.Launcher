# Final Release Size Analysis
Write-Host "=== Release Sizes After post_build ===" -ForegroundColor Cyan

$wpfTotal = (Get-ChildItem "Output/Release" -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\Avalonia\\" } | Measure-Object Length -Sum).Sum
Write-Host ("WPF Release: {0:N2} MB" -f ($wpfTotal / 1MB))

$avTotal = (Get-ChildItem "Output/Release/Avalonia" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Write-Host ("Avalonia Release: {0:N2} MB" -f ($avTotal / 1MB))

Write-Host ""
Write-Host "=== Breakdown ===" -ForegroundColor Yellow
$wpfRoot = (Get-ChildItem "Output/Release" -File | Measure-Object Length -Sum).Sum
$avRoot = (Get-ChildItem "Output/Release/Avalonia" -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Write-Host ("WPF root files: {0:N2} MB" -f ($wpfRoot / 1MB))
Write-Host ("Avalonia root files: {0:N2} MB" -f ($avRoot / 1MB))

$wpfPlugins = (Get-ChildItem "Output/Release/Plugins" -Recurse -File | Measure-Object Length -Sum).Sum
$avPlugins = (Get-ChildItem "Output/Release/Avalonia/Plugins" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Write-Host ("WPF plugins: {0:N2} MB" -f ($wpfPlugins / 1MB))
Write-Host ("Avalonia plugins: {0:N2} MB" -f ($avPlugins / 1MB))

$avRuntimes = (Get-ChildItem "Output/Release/Avalonia/runtimes" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Write-Host ("Avalonia runtimes: {0:N2} MB" -f ($avRuntimes / 1MB))
