param(
    [string]$config = "Release", 
    [string]$solution,
	[string]$targetpath
)
Write-Host "Config: $config"

function Build-Version {
	if ([string]::IsNullOrEmpty($env:APPVEYOR_BUILD_VERSION)) {
		$v = (Get-Command ${TargetPath}).FileVersionInfo.FileVersion
	} else {
        $v = $env:APPVEYOR_BUILD_VERSION
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

function Copy-Resources ($path, $config) {
    $project = "$path\Flow.Launcher"
    $output = "$path\Output"
    $target = "$output\$config"
    Copy-Item -Recurse -Force $project\Themes\* $target\Themes\
    Copy-Item -Recurse -Force $project\Images\* $target\Images\
    Copy-Item -Recurse -Force $path\Plugins\HelloWorldPython $target\Plugins\HelloWorldPython
    Copy-Item -Recurse -Force $path\JsonRPC $target\JsonRPC
    # making version static as multiple versions can exist in the nuget folder and in the case a breaking change is introduced.
    Copy-Item -Force $env:USERPROFILE\.nuget\packages\squirrel.windows\1.5.2\tools\Squirrel.exe $output\Update.exe
}

function Delete-Unused ($path, $config) {
    $target = "$path\Output\$config"
    $included = Get-ChildItem $target -Filter "*.dll"
    foreach ($i in $included){
        Remove-Item -Path $target\Plugins -Include $i -Recurse 
        Write-Host "Deleting duplicated $i"
    }
    Remove-Item -Path $target -Include "*.xml" -Recurse 
}

function Validate-Directory ($output) {
    New-Item $output -ItemType Directory -Force
}

function Zip-Release ($path, $version, $output) {
    Write-Host "Begin zip release"

    $content = "$path\Output\Release\*"
    $zipFile = "$output\Flow.Launcher-$version.zip"

    Compress-Archive -Force -Path $content -DestinationPath $zipFile

    Write-Host "End zip release"
}

function Pack-Squirrel-Installer ($path, $version, $output) {
    # msbuild based installer generation is not working in appveyor, not sure why
    Write-Host "Begin pack squirrel installer"

    $spec = "$path\Scripts\flowlauncher.nuspec"
    $input = "$path\Output\Release"

    Write-Host "Packing: $spec"
    Write-Host "Input path:  $input"
    # TODO: can we use dotnet pack here?
    nuget pack $spec -Version $version -BasePath $input -OutputDirectory $output -Properties Configuration=Release

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
    
    $file = "$output\Flow Launcher-$version.exe"
    Write-Host "Filename: $file"

    Move-Item "$output\Setup.exe" $file -Force

    Write-Host "End pack squirrel installer"
}

function IsDotNetCoreAppSelfContainedPublishEvent{
    return Test-Path $solution\Output\Release\coreclr.dll
}

function FixPublishLastWriteDateTimeError ($solutionPath) {
    #Fix error from publishing self contained app, when nuget tries to pack core dll references throws the error 'The DateTimeOffset specified cannot be converted into a Zip file timestamp' 
    gci -path "$solutionPath\Output\Release" -rec -file *.dll | Where-Object {$_.LastWriteTime -lt (Get-Date).AddYears(-20)} | %  { try { $_.LastWriteTime = '01/01/2000 00:00:00' } catch {} }
}

function Main {
    $p = Build-Path
    $v = Build-Version
    Copy-Resources $p $config

    if ($config -eq "Release"){
        
        if(IsDotNetCoreAppSelfContainedPublishEvent) {
            FixPublishLastWriteDateTimeError $p
		}
        
        Delete-Unused $p $config
        $o = "$p\Output\Packages"
        Validate-Directory $o
        # making version static as multiple versions can exist in the nuget folder and in the case a breaking change is introduced.
        Pack-Squirrel-Installer $p $v $o
    
        $isInCI = $env:APPVEYOR
        if ($isInCI) {
            Zip-Release $p $v $o
        }

        Write-Host "List output directory"
        Get-ChildItem $o
    }
}

Main