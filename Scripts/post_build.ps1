param(
    [string]$config = "Release", 
    [string]$solution = (Join-Path $PSScriptRoot ".." -Resolve)
)
Write-Host "Config: $config"

function Build-Version {
    if ([string]::IsNullOrEmpty($env:flowVersion)) {
        $targetPath = Join-Path $solution "Output/Release/Flow.Launcher.dll" -Resolve
        $v = (Get-Command ${targetPath}).FileVersionInfo.FileVersion
    }
    else {
        $v = $env:flowVersion
    }

    Write-Host "Build Version: $v"
    return $v
}

function Build-Path {
    if (![string]::IsNullOrEmpty($env:APPVEYOR_BUILD_FOLDER)) {
        $p = $env:APPVEYOR_BUILD_FOLDER
    }
    elseif (![string]::IsNullOrEmpty($solution)) {
        $p = $solution
    }
    else {
        $p = Get-Location
    }

    Write-Host "Build Folder: $p"
    Set-Location $p

    return $p
}

function Copy-Resources ($path) {
    # making version static as multiple versions can exist in the nuget folder and in the case a breaking change is introduced.
    Copy-Item -Force $env:USERPROFILE\.nuget\packages\squirrel.windows\1.5.2\tools\Squirrel.exe $path\Output\Update.exe
}

function Delete-Unused ($path, $config) {
    $target = "$path\Output\$config"
    $included = @{}
    Get-ChildItem $target -Filter "*.dll" | Get-FileHash | ForEach-Object { $included.Add($_.hash, $true) }

    $deleteList = Get-ChildItem $target\Plugins -Filter "*.dll" -Recurse | 
        Select-Object Name, VersionInfo, Directory, FullName, @{name = "hash"; expression = { (Get-FileHash $_.FullName).hash } } |
        Where-Object { $included.Contains($_.hash) }
    
    $deleteList | ForEach-Object { 
        Write-Host Deleting duplicated $_.Name with version $_.VersionInfo.FileVersion at location $_.Directory.FullName
    }
    $deleteList | Remove-Item -Path {$_.FullName} 
    
    Remove-Item -Path $target -Include "*.xml" -Recurse
}

function Remove-CreateDumpExe ($path, $config) {
    $target = "$path\Output\$config"

    $depjson = Get-Content $target\Flow.Launcher.deps.json -raw |ConvertFrom-Json -depth 32
    $depjson.targets.'.NETCoreApp,Version=v5.0/win-x64'.'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/5.0.6'.native.PSObject.Properties.Remove("createdump.exe")
    $depjson|ConvertTo-Json -Depth 32|Out-File $target\Flow.Launcher.deps.json
    Remove-Item -Path $target -Include "*createdump.exe" -Recurse
}


function Validate-Directory ($output) {
    New-Item $output -ItemType Directory -Force
}


function Pack-Squirrel-Installer ($path, $version, $output) {
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\flowlauncher.nuspec"
    $input = "$path\Output\Release"

    Write-Host "Packing: $spec"
    Write-Host "Input path:  $input"
    # making version static as multiple versions can exist in the nuget folder and in the case a breaking change is introduced.
    New-Alias Nuget $env:USERPROFILE\.nuget\packages\NuGet.CommandLine\5.4.0\tools\NuGet.exe -Force

    dotnet pack "$path\Flow.Launcher\Flow.Launcher.csproj" -p:NuspecFile=$spec -p:NuspecBasePath="$path\Output\Release" -p:PackageVersion=$version -c Release --no-build --output $output

    $nupkg = "$output\FlowLauncher.$version.nupkg"
    Write-Host "nupkg path: $nupkg"
    $icon = "$path\Flow.Launcher\Resources\app.ico"
    Write-Host "icon: $icon"
    # Squirrel.com: https://github.com/Squirrel/Squirrel.Windows/issues/369
    New-Alias Squirrel $env:USERPROFILE\.nuget\packages\squirrel.windows\1.5.2\tools\Squirrel.exe -Force
    # why we need Write-Output: https://github.com/Squirrel/Squirrel.Windows/issues/489#issuecomment-156039327
    # directory of releaseDir in squirrel can't be same as directory ($nupkg) in releasify
    $temp = "$output\Temp"

    Squirrel --releasify $nupkg --releaseDir $temp --setupIcon $icon --no-msi | Write-Output
    Move-Item $temp\* $output -Force
    Remove-Item $temp
    
    $file = "$output\Flow-Launcher-v$version.exe"
    Write-Host "Filename: $file"

    Move-Item "$output\Setup.exe" $file -Force

    Write-Host "End pack squirrel installer"
}

function Publish-Self-Contained ($p) {

    $csproj = Join-Path "$p" "Flow.Launcher/Flow.Launcher.csproj" -Resolve
    $profile = Join-Path "$p" "Flow.Launcher/Properties/PublishProfiles/Net5.0-SelfContained.pubxml" -Resolve

    # we call dotnet publish on the main project. 
    # The other projects should have been built in Release at this point.
    
    dotnet publish -c Release $csproj /p:PublishProfile=$profile
}

function Main {
    $p = Build-Path
    $v = Build-Version
    Copy-Resources $p

    if ($config -eq "Release") {
        
        Delete-Unused $p $config

        Publish-Self-Contained $p

        Remove-CreateDumpExe $p $config

        $o = "$p\Output\Packages"
        Validate-Directory $o
        Pack-Squirrel-Installer $p $v $o
    }
}

Main