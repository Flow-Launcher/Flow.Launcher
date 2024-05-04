param(
    [string]$config = "Release",
    [string]$solution = (Join-Path $PSScriptRoot ".." -Resolve),
    [string]$channel = "win-x64-prerelease",
    [string]$flowVersion = ""
)
Write-Host "Config: $config"
Write-Host "Solution: $solution"
Write-Host "Flow Version: $flowVersion"
Write-Host "Channel: $channel"

function Build-Version
{
    if ( [string]::IsNullOrEmpty($flowVersion))
    {
        $targetPath = Join-Path $solution "Output/Release/Flow.Launcher.dll" -Resolve
        $v = (Get-Command ${targetPath}).FileVersionInfo.FileVersion
    }
    else
    {
        $v = $flowVersion
    }

    Write-Host "Build Version: $v"
    return $v
}

function Build-Path
{
    if (![string]::IsNullOrEmpty($env:APPVEYOR_BUILD_FOLDER))
    {
        $p = $env:APPVEYOR_BUILD_FOLDER
    }
    elseif (![string]::IsNullOrEmpty($solution))
    {
        $p = $solution
    }
    else
    {
        $p = Get-Location
    }

    Write-Host "Build Folder: $p"
    Set-Location $p

    return $p
}


function Delete-Unused($path, $config)
{
    $target = "$path\Output\$config"
    $included = Get-ChildItem $target -Filter "*.dll"

    $hashset = @{ }

    foreach ($i in $included)
    {
        $item = if ($i.VersionInfo.FileVersion -eq $null)
        {
            [ValueTuple]::Create($i.Name, "")
        }
        else
        {
            [ValueTuple]::Create($i.Name, $i.VersionInfo.FileVersion)
        }

        $key = $hashset.Add($item, $true)
    }

    $deleteList = Get-ChildItem $target\Plugins -Filter *.dll -Recurse | Where {
        $item = if ($_.VersionInfo.FileVersion -eq $null)
        {
            [ValueTuple]::Create($_.Name, "")
        }
        else
        {
            [ValueTuple]::Create($_.Name, $_.VersionInfo.FileVersion)
        }
        $hashset.ContainsKey($item)
    }

    foreach ($i in $deleteList)
    {
        write "Deleting duplicated $($i.Name) with version $($i.VersionInfo.FileVersion) at location $($i.Directory.FullName)"
        Remove-Item -Path $i.FullName
    }
    Remove-Item -Path $target -Include "*.xml" -Recurse
}


function Validate-Directory($output)
{
    New-Item $output -ItemType Directory -Force
}


function Pack-Velopack-Installer($path, $version, $output)
{
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\flowlauncher.nuspec"
    $input = "$path\Output\$config"

    Write-Host "Packing: $spec"
    Write-Host "Input path:  $input"
    Write-Host "Output path: $output"

    $repoUrl = "https://github.com/Flow-Launcher/Prereleases"

    if ($channel -eq "stable")
    {
        $repoUrl = "https://github.com/Flow-Launcher/Flow.Launcher"
    }

    Set-Alias vpk "~/.dotnet/tools/vpk.exe"
    
    if (!(Get-Command vpk -ErrorAction SilentlyContinue))
    {
        dotnet tool install --global vpk
    }

    # Create UserData folder before Packing
    # FIXME userdata should not be created in installer version
    # New-Item -ItemType Directory -Force -Path "$input\UserData"

    vpk pack --packVersion $version --packDir $input --packId FlowLauncher --mainExe Flow.Launcher.exe --channel $channel --outputDir $output --packTitle "Flow Launcher" --icon "$input\Images\app.ico"
}

function Publish-Self-Contained($p)
{
    $csproj = Join-Path "$p" "Flow.Launcher/Flow.Launcher.csproj" -Resolve
    $profile = Join-Path "$p" "Flow.Launcher/Properties/PublishProfiles/Net7.0-SelfContained.pubxml" -Resolve

    # we call dotnet publish on the main project. 
    # The other projects should have been built in Release at this point.
    dotnet publish $csproj /p:PublishProfile=$profile
}

function Main
{
    $p = Build-Path
    $v = Build-Version

    if ($config -eq "Release")
    {
        Delete-Unused $p $config

        Publish-Self-Contained $p

        $o = "$p\Releases"
        Validate-Directory $o
        Pack-Velopack-Installer $p $v $o
    }
}

Main
