# Size analysis script
param([string]$path = ".")

function Get-FolderSize {
    param([string]$folder)
    if (Test-Path $folder) {
        $files = Get-ChildItem -Path $folder -Recurse -File -ErrorAction SilentlyContinue
        $sum = ($files | Measure-Object -Property Length -Sum).Sum
        return [math]::Round($sum / 1MB, 2)
    }
    return 0
}

function Get-LargestFiles {
    param([string]$folder, [int]$count = 30)
    if (Test-Path $folder) {
        Get-ChildItem -Path $folder -File -ErrorAction SilentlyContinue | 
            Sort-Object Length -Descending | 
            Select-Object -First $count | 
            ForEach-Object { 
                "{0,8:N2} MB  {1}" -f ($_.Length / 1MB), $_.Name 
            }
    }
}

function Get-RuntimeFolders {
    param([string]$folder)
    $runtimesPath = Join-Path $folder "runtimes"
    if (Test-Path $runtimesPath) {
        Get-ChildItem -Path $runtimesPath -Directory | ForEach-Object {
            $size = Get-FolderSize $_.FullName
            "{0,8:N2} MB  runtimes/{1}" -f $size, $_.Name
        }
    } else {
        "No runtimes folder"
    }
}

Write-Host "=== WPF Debug ===" -ForegroundColor Cyan
Write-Host "Total: $(Get-FolderSize 'Output\Debug') MB (excluding Avalonia subfolder)"
Write-Host ""
Write-Host "Largest files:"
Get-LargestFiles "Output\Debug"
Write-Host ""
Write-Host "Runtime folders:"
Get-RuntimeFolders "Output\Debug"

Write-Host ""
Write-Host "=== Avalonia Debug ===" -ForegroundColor Cyan
Write-Host "Total: $(Get-FolderSize 'Output\Debug\Avalonia') MB"
Write-Host ""
Write-Host "Largest files:"
Get-LargestFiles "Output\Debug\Avalonia"
Write-Host ""
Write-Host "Runtime folders:"
Get-RuntimeFolders "Output\Debug\Avalonia"

Write-Host ""
Write-Host "=== Release builds (if exist) ===" -ForegroundColor Cyan
if (Test-Path "Output\Release") {
    Write-Host "WPF Release: $(Get-FolderSize 'Output\Release') MB"
}
if (Test-Path "Output\Release\Avalonia") {
    Write-Host "Avalonia Release: $(Get-FolderSize 'Output\Release\Avalonia') MB"
}
