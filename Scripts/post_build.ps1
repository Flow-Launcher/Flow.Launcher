param(
    [string]$config = "Release",
    [string]$solution = (Join-Path $PSScriptRoot ".." -Resolve),
    [string]$channel = "prerelease"
)
Write-Host "Config: $config"

function Build-Version
{
    if ( [string]::IsNullOrEmpty($env:flowVersion))
    {
        $targetPath = Join-Path $solution "Output/Release/Flow.Launcher.dll" -Resolve
        $v = (Get-Command ${targetPath}).FileVersionInfo.FileVersion
    }
    else
    {
        $v = $env:flowVersion
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

function Copy-Resources($path)
{
    # making version static as multiple versions can exist in the nuget folder and in the case a breaking change is introduced.
    Copy-Item -Force $env:USERPROFILE\.nuget\packages\squirrel.windows\1.5.2\tools\Squirrel.exe $path\Output\Update.exe
}

function Delete-Unused($path, $config)
{
    $target = "$path\Output\$config"
    $included = @{ }
    Get-ChildItem $target -Filter "*.dll" | Get-FileHash | ForEach-Object {
        $hash = $_.hash
        $filename = $_.Path | Split-Path -Leaf
        $included.Add([tuple]::Create($filename, $hash), $true)
    }

    $deleteList = Get-ChildItem $target\Plugins -Filter "*.dll" -Recurse |
            Select-Object Name, VersionInfo, Directory, @{ name = "hash"; expression = { (Get-FileHash $_.FullName) } } |
            Where-Object {
                $included.Contains([tuple]::Create($_.Name, $_.hash))
            }

    $deleteList | ForEach-Object {
        Write-Host Deleting duplicated $_.Name with version $_.VersionInfo.FileVersion at location $_.Directory.FullName
    }
    $deleteList | Remove-Item -Path { $_.FullName }

    Remove-Item -Path $target -Include "*.xml" -Recurse
}

function Remove-CreateDumpExe($path, $config)
{
    $target = "$path\Output\$config"

    $depjson = Get-Content $target\Flow.Launcher.deps.json -raw
    $depjson -replace '(?s)(.createdump.exe": {.*?}.*?\n)\s*', "" | Out-File $target\Flow.Launcher.deps.json -Encoding UTF8
    Remove-Item -Path $target -Include "*createdump.exe" -Recurse
}


function Validate-Directory($output)
{
    New-Item $output -ItemType Directory -Force
}


function Pack-Squirrel-Installer($path, $version, $output)
{
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\flowlauncher.nuspec"
    $input = "$path\Output\Release"

    Write-Host "Packing: $spec"
    Write-Host "Input path:  $input"

    $repoUrl = "https://github.com/Flow-Launcher/Prereleases"
    
    if ($channel -eq "stable")
    {
        $repoUrl = "https://github.com/Flow-Launcher/Flow.Launcher"
    }
    
    vpk pack --packVersion $version --packDir $input --packId FlowLauncher --mainExe Flow.Launcher.exe --channel $channel
}

function Publish-Self-Contained($p)
{

    $csproj = Join-Path "$p" "Flow.Launcher/Flow.Launcher.csproj" -Resolve
    $profile = Join-Path "$p" "Flow.Launcher/Properties/PublishProfiles/Net7.0-SelfContained.pubxml" -Resolve

    # we call dotnet publish on the main project. 
    # The other projects should have been built in Release at this point.
    dotnet publish -c Release $csproj /p:PublishProfile=$profile
}

function Publish-Portable($outputLocation, $version)
{

    & $outputLocation\Flow-Launcher-Setup.exe --silent | Out-Null
    mkdir "$env:LocalAppData\FlowLauncher\app-$version\UserData"
    Compress-Archive -Path $env:LocalAppData\FlowLauncher -DestinationPath $outputLocation\Flow-Launcher-Portable.zip
}

function Main
{
    $p = Build-Path
    $v = Build-Version
    Copy-Resources $p

    if ($config -eq "Release")
    {

        Delete-Unused $p $config

        Publish-Self-Contained $p

        $o = "$p\Output\Packages"
        Validate-Directory $o
        Pack-Squirrel-Installer $p $v $o

        #        Publish-Portable $o $v
    }
}

Main
