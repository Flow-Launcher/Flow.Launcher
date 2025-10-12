param(
    [string]$config = "Release",
    [string]$solution = (Join-Path $PSScriptRoot ".." -Resolve)
)
Write-Host "Config: $config"

function Build-Version {
    if ([string]::IsNullOrEmpty($env:flowVersion)) {
        $targetPath = Join-Path $solution "Output/Release/Flow.Launcher.dll" -Resolve
        # Use Get-Item for reliability and ProductVersion to align with AssemblyInformationalVersion.
        $v = (Get-Item $targetPath).VersionInfo.ProductVersion
    } else {
        $v = $env:flowVersion
    }

    Write-Host "Build Version: $v"
    return $v
}

function Build-Path {
    if (![string]::IsNullOrEmpty($env:APPVEYOR_BUILD_FOLDER)) {
        $p = $env:APPVEYOR_BUILD_FOLDER
    } elseif (![string]::IsNullOrEmpty($solution)) {
        $p = $solution
    } else {
        $p = Get-Location
    }

    Write-Host "Build Folder: $p"
    Set-Location $p

    return $p
}

function Copy-Resources ($path) {
    $squirrelExe = (Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\squirrel.windows\*" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName + "\tools\Squirrel.exe"
    if (Test-Path $squirrelExe) {
        Copy-Item -Force $squirrelExe $path\Output\Update.exe
    } else {
        Write-Host "Warning: Squirrel.exe could not be found in the NuGet cache." -ForegroundColor Yellow
    }
}

function Remove-UnusedFiles ($path, $config) {
    $target = "$path\Output\$config"
    $included = Get-ChildItem $target -Filter "*.dll"
    foreach ($i in $included){
        $deleteList = Get-ChildItem $target\Plugins -Include $i.Name -Recurse | Where-Object { $_.VersionInfo.FileVersion -eq $i.VersionInfo.FileVersion }
        foreach ($fileToDelete in $deleteList) {
            # A plugin's main DLL has the same name as its parent directory. We must not delete it.
            if ($fileToDelete.Directory.Name -ne $fileToDelete.BaseName) {
                Remove-Item $fileToDelete.FullName
            }
        }
    }
    Remove-Item -Path $target -Include "*.xml" -Recurse
}

function Remove-CreateDumpExe ($path, $config) {
    $target = "$path\Output\$config"

    $depjsonPath = (Get-Item "$target\Flow.Launcher.deps.json").FullName
    if (Test-Path $depjsonPath) {
        $depjson = Get-Content $depjsonPath -raw
        $depjson -replace '(?s)(.createdump.exe": {.*?}.*?\n)\s*', "" | Out-File $depjsonPath -Encoding UTF8
    }
    Remove-Item -Path $target -Include "*createdump.exe" -Recurse
}


function Initialize-Directory ($output) {
    if (Test-Path $output) {
        Remove-Item -Recurse -Force $output
    }
    New-Item $output -ItemType Directory -Force | Out-Null
}


function New-SquirrelInstallerPackage ($path, $version, $output) {
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\flowlauncher.nuspec"
    $inputPath = "$path\Output\Release"

    Write-Host "Packing: $spec"
    Write-Host "Input path:  $inputPath"

    nuget pack $spec -Version $version -BasePath $inputPath -OutputDirectory $output -Properties Configuration=Release

    $nupkg = "$output\FlowLauncher.$version.nupkg"
    Write-Host "nupkg path: $nupkg"
    $icon = "$path\Flow.Launcher\Resources\app.ico"
    Write-Host "icon: $icon"
    
    $squirrelExe = (Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\squirrel.windows\*" -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName + "\tools\Squirrel.exe"
    if (-not (Test-Path $squirrelExe)) {
        Write-Host "FATAL: Squirrel.exe could not be found, aborting installer creation." -ForegroundColor Red
        exit 1
    }
    New-Alias Squirrel $squirrelExe -Force
    
    $temp = "$output\Temp"

    Squirrel --releasify $nupkg --releaseDir $temp --setupIcon $icon --no-msi | Write-Output
    Move-Item $temp\* $output -Force
    Remove-Item $temp

    $file = "$output\Flow-Launcher-Setup.exe"
    Write-Host "Filename: $file"

    Move-Item "$output\Setup.exe" $file -Force

    Write-Host "End pack squirrel installer"
}

function Build-Solution ($p) {
    Write-Host "Building solution..."
    $solutionFile = Join-Path $p "Flow.Launcher.sln"
    dotnet build $solutionFile -c Release
    if ($LASTEXITCODE -ne 0) { return $false }
    return $true
}

function Publish-SelfContainedToTemp($p, $outputPath) {
    Write-Host "Publishing self-contained application to temporary directory..."
    $csproj  = Join-Path "$p" "Flow.Launcher/Flow.Launcher.csproj" -Resolve
    
    # We publish to a temporary directory first to ensure a clean, self-contained build 
    # without interfering with the main /Output/Release folder, which contains plugins.
    # Let publish do its own build to ensure all self-contained dependencies are correctly resolved.
    dotnet publish -c Release $csproj -r win-x64 --self-contained true -o $outputPath
    if ($LASTEXITCODE -ne 0) { return $false }
    return $true
}

function Merge-PublishToRelease($publishPath, $releasePath) {
    Write-Host "Merging published files into release directory..."
    Copy-Item -Path "$publishPath\*" -Destination $releasePath -Recurse -Force
    Remove-Item -Recurse -Force $publishPath
}

function Publish-Portable ($outputLocation, $version, $path) {
    # The portable version is created by silently running the installer to a temporary location, 
    # then packaging the result. This ensures the structure is identical to a real installation 
    # and can be updated by Squirrel.
    & "$outputLocation\Flow-Launcher-Setup.exe" --silent | Out-Null
    
    $installRoot = Join-Path $env:LocalAppData "FlowLauncher"
    $appPath = Join-Path $installRoot "app-$version"
    $appExePath = Join-Path $appPath "Flow.Launcher.exe"

    # Wait for silent installation to complete
    $waitTime = 0
    $maxWaitTime = 60 # 60 seconds timeout
    while (-not (Test-Path $appExePath) -and $waitTime -lt $maxWaitTime) {
        Start-Sleep -Seconds 1
        $waitTime++
    }
    
    if (-not (Test-Path $appExePath)) {
        Write-Host "Error: Timed out waiting for silent installation to complete." -ForegroundColor Red
        return
    }

    # Create a temporary staging directory for the portable package
    $stagingDir = Join-Path $outputLocation "PortableStaging"
    Initialize-Directory $stagingDir

    try {
        # Copy installed files to staging directory
        Copy-Item -Path "$installRoot\*" -Destination $stagingDir -Recurse -Force
        
        # Create the UserData folder inside app-<version> to enable portable mode.
        New-Item -Path (Join-Path $stagingDir "app-$version" "UserData") -ItemType Directory -Force | Out-Null
        
        # Remove the unnecessary 'packages' directory before creating the archive
        $packagesPath = Join-Path $stagingDir "packages"
        if (Test-Path $packagesPath) {
            Remove-Item -Path $packagesPath -Recurse -Force
        }

        # Create the zip from the staging directory's contents
        Compress-Archive -Path "$stagingDir\*" -DestinationPath "$outputLocation\Flow-Launcher-Portable.zip" -Force
    }
    finally {
        if (Test-Path $stagingDir) {
            Remove-Item -Recurse -Force $stagingDir
        }
    }
    
    # Uninstall after packaging
    $uninstallExe = Join-Path $installRoot "Update.exe"
    if (Test-Path $uninstallExe) {
        Write-Host "Uninstalling temporary application..."
        Start-Process -FilePath $uninstallExe -ArgumentList "--uninstall -s" -Wait
    }
}

function Main {
    $p = Build-Path

    if ($config -eq "Release"){

        if (-not (Build-Solution $p)) {
            Write-Host "dotnet build failed. Aborting post-build script." -ForegroundColor Red
            exit 1
        }
        
        $tempPublishPath = Join-Path $p "Output\PublishTemp"
        Initialize-Directory $tempPublishPath
        if (-not (Publish-SelfContainedToTemp $p $tempPublishPath)) {
            Write-Host "dotnet publish failed. Aborting." -ForegroundColor Red
            exit 1
        }

        Merge-PublishToRelease $tempPublishPath (Join-Path $p "Output\Release")
        
        $v = Build-Version
        if ([string]::IsNullOrEmpty($v)) { 
            Write-Host "Could not determine build version. Aborting." -ForegroundColor Red
            exit 1
        }

        Copy-Resources $p
        Remove-UnusedFiles $p $config
        Remove-CreateDumpExe $p $config

        $o = "$p\Output\Packages"
        Initialize-Directory $o
        New-SquirrelInstallerPackage $p $v $o
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Squirrel packaging failed. Aborting." -ForegroundColor Red
            exit 1
        }

        Publish-Portable $o $v $p
    }
}

Main