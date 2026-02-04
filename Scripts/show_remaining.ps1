# Show remaining duplicates with details
Write-Host "=== Remaining Duplicate DLLs in Avalonia Plugins ===" -ForegroundColor Cyan

$avRootDlls = Get-ChildItem "Output/Debug/Avalonia" -Filter "*.dll"

$remaining = @()

Get-ChildItem "Output/Debug/Avalonia/Plugins" -Directory | ForEach-Object {
    $pluginName = $_.Name
    
    Get-ChildItem $_.FullName -Recurse -Filter "*.dll" | ForEach-Object {
        $pluginDll = $_
        $matchingRoot = $avRootDlls | Where-Object { $_.Name -eq $pluginDll.Name }
        if ($matchingRoot) {
            # Check if version matches
            $rootVersion = $matchingRoot.VersionInfo.FileVersion
            $pluginVersion = $pluginDll.VersionInfo.FileVersion
            $remaining += [PSCustomObject]@{
                Plugin = $pluginName
                DLL = $pluginDll.Name
                SizeMB = [math]::Round($pluginDll.Length / 1MB, 2)
                RootVersion = $rootVersion
                PluginVersion = $pluginVersion
                SameVersion = ($rootVersion -eq $pluginVersion)
            }
        }
    }
}

$remaining | Sort-Object SizeMB -Descending | Format-Table -AutoSize
