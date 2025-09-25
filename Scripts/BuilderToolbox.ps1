# .NET Builder Toolbox
# Clean, build, and manage .NET solutions.
# -----------------------------------------------
# Edit "Global Variables" to adapt for your project.
# Most paths are relative to the script's location, and assume it's in a root subfolder.
# -----------------------------------------------

# --- Global Variables ---
# Project-specific settings to be configured.
$global:appName = "Flow.Launcher"
$global:processNameForTermination = "Flow.Launcher"
$global:solutionRoot = (Get-Item $PSScriptRoot).Parent.FullName
$global:solutionFile = Join-Path $solutionRoot "Flow.Launcher.sln"
$global:mainProjectFile = Join-Path $solutionRoot $appName "$($appName).csproj"
$global:assemblyInfoPath = Join-Path $solutionRoot "SolutionAssemblyInfo.cs"
$global:requiredDotNetVersion = "9" # Major version number
$global:publishRuntimeId = "win-x64" # Used for portable package publish

# Velopack settings
$global:requiredVpkVersion = "0.0.1362-gfa48e3c"
$global:packageId = "FlowLauncher" # Velopack package ID
$global:packageTitle = "Flow Launcher"
$global:packageAuthors = "Flow-Launcher Team"
$global:packageIconPath = Join-Path $solutionRoot "Flow.Launcher/Resources/app.ico"
$global:mainExeName = "Flow.Launcher.exe"

# Post-build customization
$global:runPostBuildCleanup = $true # Run custom cleanup tasks
$global:pluginsSubfolder = "Plugins" # Subfolder to clean unused DLLs from
$global:removeCreateDump = $true # Remove createdump.exe from deps.json
$global:removeXmlFiles = $true # Remove *.xml doc files from output

# 7-Zip
$global:sevenZipPath = Join-Path $PSScriptRoot "7z/7za.exe"

# Menu item definitions for layout and logging
$global:menuItems = @{
    "1" = "Build & Run (Debug)"; "A" = "Open Output Folder"
    "2" = "Build & Run (Release)"; "B" = "Open Solution in IDE"
    "3" = "Watch & Run (Hot Reload)"; "C" = "Clean Solution"
    "4" = "Run Tests"; "D" = "Clean Logs"
    "5" = "Publish Portable Package"; "E" = "Change Version Number"
    "6" = "Publish Production Package"; "F" = "Open appveyor.yml"
    "7" = "List Packages + Updates"; "G" = "Open User Data folder"
    "8" = "Restore NuGet Packages"; "H" = "Open log file"
    "Q" = "Quit"
}
$global:logFile = $null
$global:sdkVersion = "N/A"
$global:gitBranch = ""
$global:gitCommit = ""


# --- Logging ---
function Get-LogFile {
    if ($null -eq $global:logFile) {
        $global:logFile = Start-Logging
    }
    return $global:logFile
}


function Start-Logging {
    $logDir = Join-Path $PSScriptRoot "Logs"
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir | Out-Null
    }
    $logFile = Join-Path $logDir "$($global:appName).build.$((Get-Date).ToString('yyyyMMdd.HHmmss')).log"

    $appVersion = Get-BuildVersion
    if ([string]::IsNullOrEmpty($appVersion)) { $appVersion = "N/A" }

    # Create a transcript header
    $header = @"
***************************************
$($global:appName), version = $appVersion
Log Start: $((Get-Date).ToString('yyyyMMddHHmmss'))
Username:   $($env:USERDOMAIN)\$($env:USERNAME)
***************************************
"@
    Set-Content -Path $logFile -Value $header

    return $logFile
}


function Clear-Logs {
    Write-Host "Cleaning logs..." -ForegroundColor Yellow
    $logDir = Join-Path $PSScriptRoot "Logs"
    if (Test-Path $logDir) {
        $logFiles = Get-ChildItem -Path $logDir -Filter "*.log"
        if ($null -ne $global:logFile) {
            # Exclude the current session's log file from deletion
            $logFiles = $logFiles | Where-Object { $_.FullName -ne $global:logFile }
        }

        if ($logFiles) {
            Write-Host "Removing $($logFiles.Count) old log files from $logDir"
            Remove-Item -Path $logFiles.FullName -Force -ProgressAction SilentlyContinue
        }
        else {
            Write-Host "No old log files to clean."
        }
    }
    else {
        Write-Host "Log directory not found."
    }
    Write-Host "Log cleanup complete." -ForegroundColor Green
}


# --- Prerequisite Check ---
function Test-DotNetVersion {
    # This function now only writes to console and returns an object with success status and a message.
    # It does not log.
    Write-Host "Checking for .NET $($global:requiredDotNetVersion) SDK..." -NoNewline
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        $msg = " 'dotnet.exe' not found. Ensure .NET $($global:requiredDotNetVersion) SDK is installed and in your PATH."
        Write-Host $msg -ForegroundColor Red
        return [PSCustomObject]@{ Success = $false; Message = $msg }
    }

    $version_output = (dotnet --version)
    if ($version_output -match "^$($global:requiredDotNetVersion)\.") {
        Write-Host " Found version $version_output." -ForegroundColor Green
        return [PSCustomObject]@{ Success = $true; Message = "Found version $version_output." }
    }
    else {
        $msg = " Required version $($global:requiredDotNetVersion).*, but found $version_output. Please install the correct SDK."
        Write-Host $msg -ForegroundColor Red
        return [PSCustomObject]@{ Success = $false; Message = $msg }
    }
}


function Test-VpkVersion {
    Write-Host "Checking VPK version..."
    $installedVersion = ""
    try {
        $vpkLine = dotnet tool list -g | Where-Object { $_ -match "^vpk\s+" }
        if ($vpkLine) {
            $installedVersion = ($vpkLine -split '\s+')[1].Trim()
        }
    }
    catch {}

    if ($installedVersion -ne $requiredVpkVersion) {
        Write-Host "VPK wrong version. Required: $requiredVpkVersion. Found: '$installedVersion'" -ForegroundColor Yellow
        Write-Host "Installing correct version..."
        Invoke-DotnetCommand -Command "tool" -Arguments "uninstall -g vpk" -IgnoreErrors $true
        if (-not (Invoke-DotnetCommand -Command "tool" -Arguments "install -g vpk --version $requiredVpkVersion")) {
            Write-Host "Failed to install required VPK version." -ForegroundColor Red
            return $false
        }
        Write-Host "VPK version $requiredVpkVersion installed successfully." -ForegroundColor Green
    }
    else {
        Write-Host "VPK version $requiredVpkVersion is already installed." -ForegroundColor Green
    }
    return $true
}


# --- Command Execution Helper ---
function Invoke-DotnetCommand {
    param(
        [string]$Command,
        [string]$Arguments,
        [switch]$IgnoreErrors
    )
    $invokeParams = @{
        ExecutablePath = "dotnet"
        Arguments      = "$Command $Arguments --verbosity normal"
    }
    if ($IgnoreErrors) {
        $invokeParams.Add("IgnoreErrors", $true)
    }
    Invoke-ExternalCommand @invokeParams
}


function Invoke-ExternalCommand {
    param(
        [string]$ExecutablePath,
        [string]$Arguments,
        [string]$WorkingDirectory = "",
        [switch]$IgnoreErrors
    )
    $logFile = Get-LogFile
    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $ExecutablePath
    $processInfo.Arguments = $Arguments
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    if (-not [string]::IsNullOrEmpty($WorkingDirectory)) {
        $processInfo.WorkingDirectory = $WorkingDirectory
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processInfo
    
    $stdOutHandler = { if (-not [string]::IsNullOrEmpty($EventArgs.Data)) { $EventArgs.Data | Out-File -FilePath $logFile -Append } }
    $stdErrHandler = { if (-not [string]::IsNullOrEmpty($EventArgs.Data)) { "ERROR: $($EventArgs.Data)" | Out-File -FilePath $logFile -Append } }
    
    $stdOutEvent = Register-ObjectEvent -InputObject $process -EventName "OutputDataReceived" -Action $stdOutHandler
    $stdErrEvent = Register-ObjectEvent -InputObject $process -EventName "ErrorDataReceived" -Action $stdErrHandler

    try {
        $process.Start() | Out-Null
    }
    catch {
        Write-Host "Error starting process '$ExecutablePath': $_" -ForegroundColor Red
        return $false
    }
    
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()

    while (-not $process.HasExited) {
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 1
    }
    $process.WaitForExit()

    # Always write a newline after the progress dots to clean up the console line.
    Write-Host ""

    Unregister-Event -SubscriptionId $stdOutEvent.Id
    Unregister-Event -SubscriptionId $stdErrEvent.Id
    
    return ($process.ExitCode -eq 0 -or $IgnoreErrors)
}


function Invoke-AndTee-Command {
    param(
        [string]$ExecutablePath,
        [string]$Arguments
    )
    $logFile = Get-LogFile
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $ExecutablePath
    $pinfo.Arguments = $Arguments
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true

    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $pinfo
    $p.Start() | Out-Null
    
    $output = $p.StandardOutput.ReadToEnd()
    $errors = $p.StandardError.ReadToEnd()
    $p.WaitForExit()

    if ($output) {
        Write-Host $output
        $output | Out-File -FilePath $logFile -Append
    }
    if ($errors) {
        Write-Host $errors -ForegroundColor Red
        $errors | Out-File -FilePath $logFile -Append
    }
    return $p.ExitCode
}


# --- Helpers ---
function Get-BuildVersion {
    if (-not (Test-Path $global:assemblyInfoPath)) {
        Write-Host "Warning: Assembly info file not found at '$($global:assemblyInfoPath)'. Version cannot be determined." -ForegroundColor Yellow
        return $null
    }
    $content = Get-Content $global:assemblyInfoPath -Raw
    $informationalVersion = ($content | Select-String 'AssemblyInformationalVersion\("([^"]+)"\)' | ForEach-Object { $_.Matches.Groups[1].Value })
    if (-not [string]::IsNullOrEmpty($informationalVersion)) {
        return $informationalVersion
    }
    # Fallback to AssemblyVersion if Informational is not found.
    $assemblyVersion = ($content | Select-String 'AssemblyVersion\("([^"]+)"\)' | ForEach-Object { $_.Matches.Groups[1].Value })
    return $assemblyVersion
}


function Confirm-ProcessTermination {
    param([string]$Action = "Build")
    $process = Get-Process -Name $global:processNameForTermination -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "$($global:processNameForTermination) is running (PID: $($process.Id))." -ForegroundColor Yellow
        $kill = Read-Host "Do you want to terminate it? (y/n)"
        if ($kill.ToLower() -eq 'y') {
            Stop-Process -Name $global:processNameForTermination -Force
            Write-Host "$($global:processNameForTermination) terminated."
            Start-Sleep -Seconds 1
        }
        else {
            Write-Host "$($Action) aborted." -ForegroundColor Red
            return $false
        }
    }
    return $true
}


function Invoke-BuildAndStage {
    param(
        [string]$PublishDir,
        [bool]$IsSelfContained
    )
    
    Remove-BuildOutput -NoConfirm
    
    Write-Host "Building entire solution to ensure plugins are available..."
    if (-not (Invoke-DotnetCommand -Command "build" -Arguments "`"$solutionFile`" -c Release")) {
        Write-Host "ERROR! Solution build failed." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
        return $null
    }
    
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force -ProgressAction SilentlyContinue
    }

    $arguments = "publish `"$($global:mainProjectFile)`" -c Release -r $($global:publishRuntimeId) --self-contained $IsSelfContained -o `"$PublishDir`""
    if (-not (Invoke-DotnetCommand -Command "" -Arguments $arguments)) {
        Write-Host "ERROR! Publish failed." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
        return $null
    }
    
    Write-Host "Copying plugins and preparing data folder..."
    $pluginsSourceDir = Join-Path $global:solutionRoot "Output\Release\Plugins"
    $pluginsDestDir = Join-Path $PublishDir "Plugins"
    Copy-Item -Path $pluginsSourceDir -Destination $pluginsDestDir -Recurse -Force -ProgressAction SilentlyContinue
    New-Item -Path (Join-Path $PublishDir "UserData") -ItemType Directory -Force | Out-Null

    return $PublishDir
}

function Invoke-ItemSafely {
    param(
        [string]$Path,
        [string]$ItemType = "Item"
    )
    if (-not (Test-Path $Path)) {
        Write-Host "Error: Could not find $ItemType at '$Path'." -ForegroundColor Red
        Read-Host "Press ENTER to continue..."
    }
    else {
        Invoke-Item $Path -ErrorAction SilentlyContinue
    }
}


function New-ChangelogFromGit {
    param([string]$OutputDir)
    Write-Host "Generating changelog from Git history..."
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host "Warning: git.exe not found. Skipping changelog generation." -ForegroundColor Yellow
        return
    }

    try {
        $latestTag = (git -C $global:solutionRoot describe --tags --abbrev=0 2>$null).Trim()
        if ([string]::IsNullOrEmpty($latestTag)) { throw "No tags found." }
        Write-Host "Found latest tag: '$latestTag'. Generating changelog from new commits."
        $commitRange = "$latestTag..HEAD"
        $header = "## Changes since $latestTag"
    }
    catch {
        Write-Host "No Git tags found. Generating changelog for all commits." -ForegroundColor Yellow
        $commitRange = "HEAD"
        $header = "## Full Project Changelog"
    }

    try {
        $gitLogCommand = "log $commitRange --pretty=format:'* %h - %s (%an)'"
        $changelogContent = (git -C $global:solutionRoot $gitLogCommand)

        if ([string]::IsNullOrWhiteSpace(($changelogContent -join ''))) {
            Write-Host "No new commits to add to changelog. File not created." -ForegroundColor Yellow
            return
        }

        $fullContent = "$header`n`n$($changelogContent -join "`n")"
        $outputPath = Join-Path $OutputDir "Changelog.md"
        Set-Content -Path $outputPath -Value $fullContent
        Write-Host "Changelog.md created successfully." -ForegroundColor Green
    }
    catch {
        Write-Host "Warning: Failed to generate changelog from Git history. $_" -ForegroundColor Yellow
    }
}


# --- Core Functions (Menu Actions) ---
function Start-BuildAndRun {
    param($Configuration)
    if (-not (Confirm-ProcessTermination)) { return }

    Write-Host "Building solution in $Configuration mode..."
    # Step 1: Build the solution quietly. Output goes to the log file.
    if (-not (Invoke-DotnetCommand -Command "build" -Arguments "`"$solutionFile`" -c $Configuration")) {
        Write-Host "Build failed. See $(Get-LogFile) for details." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
        return
    }

    Write-Host "Build successful. Starting application..."
    # Step 2: Run the application's DLL using the dotnet host to avoid runtime issues.
    $dllPath = Join-Path $solutionRoot "Output/$Configuration/$($global:appName).dll"
    if (Test-Path $dllPath) {
        $arguments = "`"$dllPath`""
        "dotnet $arguments" | Out-File -FilePath (Get-LogFile) -Append
        Start-Process "dotnet" -ArgumentList $arguments -WindowStyle Hidden
        Write-Host "Application started." -ForegroundColor Green
    }
    else {
        Write-Host "ERROR! Main application DLL not found at $dllPath" -ForegroundColor Red
    }
}


function Watch-And-Run {
    if (-not (Confirm-ProcessTermination)) { return }
    Write-Host "Starting dotnet watch. Press CTRL+C in the new window to stop."
    $arguments = "watch --project `"$($global:mainProjectFile)`" run"
    "dotnet $arguments" | Out-File -FilePath (Get-LogFile) -Append
    Start-Process "dotnet" -ArgumentList $arguments
}


function Restore-NuGetPackages {
    Write-Host "Restoring NuGet packages..."
    if (Invoke-DotnetCommand -Command "restore" -Arguments "`"$global:solutionFile`"") {
        Write-Host "NuGet packages restored successfully." -ForegroundColor Green
    }
    else {
        Write-Host "ERROR! NuGet packages not restored." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
    }
}


function Invoke-UnitTests {
    Write-Host "Running unit tests..."
    if (Invoke-DotnetCommand -Command "test" -Arguments "`"$solutionFile`"") {
        Write-Host "All tests passed." -ForegroundColor Green
    }
    else {
        Write-Host "ERROR! Some tests failed." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
    }
}


function Publish-Portable {
    if (-not (Confirm-ProcessTermination -Action "Publish")) { return }

    $publishDir = Join-Path $solutionRoot "Output\Portable_Staging"
    $publishDir = Invoke-BuildAndStage -PublishDir $publishDir -IsSelfContained $true
    if ($null -eq $publishDir) { return }

    Write-Host "Post-build processing..."
    if ($global:runPostBuildCleanup) {
        Remove-UnusedPluginFiles -Path $publishDir -LogFile (Get-LogFile)
    }
    if ($global:removeCreateDump) {
        Remove-CreateDumpReference -Path $publishDir
    }

    Write-Host "Removing debug symbols (*.pdb)..."
    Get-ChildItem -Path $publishDir -Include "*.pdb" -Recurse -ProgressAction SilentlyContinue | Remove-Item -Force -ProgressAction SilentlyContinue

    Write-Host "Archiving portable package..."
    if (-not (Test-Path $global:sevenZipPath)) {
        Write-Host "ERROR! 7za.exe not found at $($global:sevenZipPath)"; Invoke-Item (Get-LogFile); return
    }
    
    $finalOutputDir = Join-Path $solutionRoot "Output"
    $destinationArchive = Join-Path $finalOutputDir "$($global:appName)-Portable.7z"
    if (Test-Path $destinationArchive) { Remove-Item $destinationArchive -Force -ProgressAction SilentlyContinue }
    
    $sourceDir = Join-Path $publishDir "*"
    $7zArgs = "a -t7z -m0=lzma2 -mx=3 `"$destinationArchive`" `"$sourceDir`""
    
    if (-not (Invoke-ExternalCommand -ExecutablePath $global:sevenZipPath -Arguments $7zArgs)) {
        Write-Host "ERROR! 7-Zip archiving failed."; Invoke-Item (Get-LogFile); return
    }
    
    Remove-Item $publishDir -Recurse -Force -ProgressAction SilentlyContinue
    New-ChangelogFromGit -OutputDir $finalOutputDir

    Write-Host "Portable archive created: $destinationArchive" -ForegroundColor Green
    Invoke-Item $finalOutputDir
}


function Build-ProductionPackage {
    if (-not (Confirm-ProcessTermination -Action "Production Build")) { return }
    if (-not (Test-VpkVersion)) { return }

    $v = Get-BuildVersion
    if ([string]::IsNullOrEmpty($v)) { 
        Write-Host "Could not determine version from $($global:assemblyInfoPath)" -ForegroundColor Red
        return 
    }

    $publishDir = Join-Path $global:solutionRoot "Output\Publish"
    $publishDir = Invoke-BuildAndStage -PublishDir $publishDir -IsSelfContained $true
    if ($null -eq $publishDir) { return }
    
    Write-Host "Post-build processing..."
    if ($global:runPostBuildCleanup) {
        Remove-UnusedPluginFiles -Path $publishDir -LogFile (Get-LogFile)
    }
    if ($global:removeCreateDump) {
        Remove-CreateDumpReference -Path $publishDir
    }

    $packagesDir = Join-Path $solutionRoot "Output\Packages"
    if (Test-Path $packagesDir) { Remove-Item $packagesDir -Recurse -Force -ProgressAction SilentlyContinue }
    New-Item $packagesDir -ItemType Directory -Force | Out-Null
    
    Write-Host "Packaging installer and portable..."
    if (-not (New-VelopackPackage -Version $v -OutputDir $packagesDir -InputPath $publishDir)) { 
        Write-Host "ERROR! Velopack packaging failed."; return 
    }

    Remove-Item $publishDir -Recurse -Force -ProgressAction SilentlyContinue
    New-ChangelogFromGit -OutputDir $packagesDir
    
    Write-Host "Production build complete: $packagesDir" -ForegroundColor Green
    Invoke-Item $packagesDir
}


function Get-OutdatedPackages {
    Write-Host "Ensuring packages are restored first..."
    if (-not (Invoke-DotnetCommand -Command "restore" -Arguments "`"$global:solutionFile`"")) {
        Write-Host "ERROR! Package restore failed." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
        return
    }

    Write-Host "Checking for outdated NuGet packages..."
    if (Invoke-DotnetCommand -Command "list" -Arguments "`"$global:solutionFile`" package --outdated") {
        Write-Host "Check complete. See log." -ForegroundColor Green
        Invoke-Item (Get-LogFile)
    }
    else {
        Write-Host "ERROR! Packages check has failed. See log." -ForegroundColor Red
        Invoke-Item (Get-LogFile)
    }
}


function Remove-BuildOutput {
    param([switch]$NoConfirm)
    if (-not $NoConfirm -and -not (Confirm-ProcessTermination -Action "Clean")) {
        return
    }

    Write-Host "Cleaning build files..." -ForegroundColor Yellow
    $outputDir = Join-Path $solutionRoot "Output"
    if (Test-Path $outputDir) {
        Write-Host "Removing $outputDir"
        Remove-Item -Recurse -Force $outputDir -ProgressAction SilentlyContinue
    }
    # The -ProgressAction parameter is the definitive way to suppress the progress stream
    # that was causing the blank lines, ensuring the command is truly silent.
    Get-ChildItem -Path $solutionRoot -Include bin, obj -Recurse -Directory -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
    Write-Host "Cleanup complete." -ForegroundColor Green
}


function Update-VersionNumber {
    if (-not(Test-Path $assemblyInfoPath)) {
        Write-Host "ERROR! Assembly info file not found at: $assemblyInfoPath" -ForegroundColor Red
        return
    }
    $content = Get-Content $assemblyInfoPath -Raw
    $currentVersion = ($content | Select-String 'AssemblyInformationalVersion\("([^"]+)"\)' | ForEach-Object { $_.Matches.Groups[1].Value })
    Write-Host "Current version: $currentVersion" -ForegroundColor Yellow

    $newVersion = Read-Host "Enter new version, or 'X' to cancel"
    if ([string]::IsNullOrWhiteSpace($newVersion) -or $newVersion -eq 'X' -or $newVersion -eq 'x') {
        Write-Host "Operation cancelled."; return
    }

    try {
        $parsedVersion = [System.Version]::new($newVersion.Split('-')[0])
        $fileVersion = "{0}.{1}.{2}" -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build

        $content = $content -replace '(AssemblyVersion\(")([^"]+)("\))', ('${1}' + $fileVersion + '${3}')
        $content = $content -replace '(AssemblyFileVersion\(")([^"]+)("\))', ('${1}' + $fileVersion + '${3}')
        $content = $content -replace '(AssemblyInformationalVersion\(")([^"]+)("\))', ('${1}' + $newVersion + '${3}')
        
        Set-Content -Path $assemblyInfoPath -Value $content
        Write-Host "Version updated to $newVersion in $assemblyInfoPath" -ForegroundColor Green
        
        $logFile = Get-LogFile
        "Version updated from '$currentVersion' to '$newVersion' in $assemblyInfoPath." | Out-File -FilePath $logFile -Append
    }
    catch {
        Write-Host "ERROR! Invalid version. Use Semantic Versioning - Major.Minor.Patch - e.g., 1.2.4." -ForegroundColor Red
    }
}


function Open-LatestLogFile {
    $logDir = Join-Path $PSScriptRoot "Logs"
    $latestLog = Get-ChildItem -Path $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Invoke-ItemSafely -Path $latestLog.FullName -ItemType "Log file"
    }
    else {
        Write-Host "No logs found to open." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}


function Open-UserDataFolder {
    $userDataPath = Join-Path $env:APPDATA 'FlowLauncher'
    Invoke-ItemSafely -Path $userDataPath -ItemType "User data folder"
}


# --- Post-Build Functions ---
function Remove-UnusedPluginFiles {
    param($Path, $LogFile)
    Write-Host "Removing unused plugin files..." -ForegroundColor Cyan
    $included = Get-ChildItem $Path -Filter "*.dll"
    foreach ($i in $included) {
        $deleteList = Get-ChildItem (Join-Path $Path $global:pluginsSubfolder) -Include $i.Name -Recurse | Where-Object { $_.VersionInfo.FileVersion -eq $i.VersionInfo.FileVersion }
        foreach ($fileToDelete in $deleteList) {
            # A plugin's main DLL has the same name as its parent directory. We must not delete it.
            if ($fileToDelete.Directory.Name -ne $fileToDelete.BaseName) {
                "Deleting duplicated $($fileToDelete.Name) with version $($fileToDelete.VersionInfo.FileVersion) at location $($fileToDelete.Directory.FullName)" | Out-File -FilePath $LogFile -Append
                Remove-Item $fileToDelete.FullName
            }
        }
    }
    if ($global:removeXmlFiles) {
        Remove-Item -Path $Path -Include "*.xml" -Recurse
    }
}


function Remove-CreateDumpReference {
    param($Path)
    Write-Host "Removing createdump reference..." -ForegroundColor Cyan
    $depjsonPath = Join-Path $Path "$($global:appName).deps.json"
    if (Test-Path $depjsonPath) {
        $depjson = Get-Content $depjsonPath -raw
        $depjson -replace '(?s)(.createdump.exe": {.*?}.*?\n)\s*', "" | Out-File $depjsonPath -Encoding UTF8
    }
    Remove-Item -Path $Path -Include "*createdump.exe" -Recurse
}


function New-VelopackPackage {
    param($Version, $OutputDir, $InputPath)
    Write-Host "Begin packing Velopack installer"
    
    # Do not provide '--framework' to Velopack if the input is self-contained.
    # Velopack is smart enough to create a framework-dependent installer from a self-contained input.
    $vpkArgs = "pack -u $($global:packageId) -v $Version -p `"$InputPath`" -o `"$OutputDir`" --mainExe `"$($global:mainExeName)`" --packTitle `"$global:packageTitle`" --packAuthors `"$global:packageAuthors`" --icon `"$global:packageIconPath`""
    if (-not (Invoke-ExternalCommand -ExecutablePath "vpk" -Arguments $vpkArgs)) {
        Write-Host "ERROR! Velopack packaging failed." -ForegroundColor Red
        return $false
    }
    
    $installer = Get-ChildItem -Path $OutputDir -Filter "*Setup.exe" | Select-Object -First 1
    if ($installer) {
        Rename-Item $installer.FullName (Join-Path $OutputDir "$($global:appName)-Setup.exe") -Force *> $null
    }

    $velopackPortableZip = Get-ChildItem -Path $OutputDir -Filter "*-portable.zip" | Select-Object -First 1
    if ($velopackPortableZip) {
        Rename-Item $velopackPortableZip.FullName (Join-Path $OutputDir "$($global:appName)-Portable.zip") -Force *> $null
    }

    Write-Host "End pack velopack installer"
    return $true
}


# --- Menu Display ---
function Show-Menu {
    param([string]$LogFile)
    # This function is now only responsible for writing the menu text.
    # Screen management is handled by the main loop.
    
    $appVersion = Get-BuildVersion
    if ([string]::IsNullOrEmpty($appVersion)) { $appVersion = "N/A (check $($global:assemblyInfoPath))" }
    
    $logFileName = "Not yet created."
    if (-not [string]::IsNullOrEmpty($LogFile)) {
        $logFileName = Split-Path $LogFile -Leaf
    }
    
    Write-Host "-----------------------------------------------------------------------" -ForegroundColor Green
    Write-Host "              .NET Builder Toolbox for $($global:appName)" -ForegroundColor Green
    Write-Host "-----------------------------------------------------------------------" -ForegroundColor Green
    Write-Host "Solution: $($global:solutionFile)" -ForegroundColor DarkGray
    if (-not [string]::IsNullOrEmpty($global:gitBranch)) {
        Write-Host "Branch:   $($global:gitBranch) ($($global:gitCommit))" -ForegroundColor DarkGray
    }
    Write-Host "Version:  $appVersion" -ForegroundColor DarkGray
    Write-Host "SDK:      $($global:sdkVersion)" -ForegroundColor DarkGray
    Write-Host "Logging:  $logFileName" -ForegroundColor DarkGray
    Write-Host "-----------------------------------------------------------------------" -ForegroundColor Green

    $menuTable = @(
        [PSCustomObject]@{ Left = "1. $($global:menuItems['1'])"; Right = "A. $($global:menuItems['A'])" }
        [PSCustomObject]@{ Left = "2. $($global:menuItems['2'])"; Right = "B. $($global:menuItems['B'])" }
        [PSCustomObject]@{ Left = "3. $($global:menuItems['3'])"; Right = "C. $($global:menuItems['C'])" }
        [PSCustomObject]@{ Left = "4. $($global:menuItems['4'])"; Right = "D. $($global:menuItems['D'])" }
        [PSCustomObject]@{ Left = "5. $($global:menuItems['5'])"; Right = "E. $($global:menuItems['E'])" }
        [PSCustomObject]@{ Left = "6. $($global:menuItems['6'])"; Right = "F. $($global:menuItems['F'])" }
        [PSCustomObject]@{ Left = "7. $($global:menuItems['7'])"; Right = "G. $($global:menuItems['G'])" }
        [PSCustomObject]@{ Left = "8. $($global:menuItems['8'])"; Right = "H. $($global:menuItems['H'])" }
    )
    # Trim the output of Format-Table to remove leading/trailing blank lines
    ($menuTable | Format-Table -HideTableHeaders -AutoSize | Out-String).Trim() | Write-Host
    
    Write-Host "Q. $($global:menuItems['Q'])" -ForegroundColor Magenta
    Write-Host "-----------------------------------------------------------------------" -ForegroundColor Green
    Write-Host "Run option:" -ForegroundColor Cyan -NoNewline
}


# --- Main Execution Logic ---
# Pre-execution check to ensure script is in the right location.
if (-not (Test-Path $global:solutionFile)) {
    Write-Host "ERROR! Solution file not found at '$($global:solutionFile)'. Ensure this script is in the correct project 'Scripts' directory." -ForegroundColor Red
    Read-Host "ENTER to exit..."
    return
}

try {
    # --- Pre-run Info Gathering ---
    $global:sdkVersion = (dotnet --version 2>$null).Trim()
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $global:gitBranch = (git -C $global:solutionRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
        $global:gitCommit = (git -C $global:solutionRoot rev-parse --short HEAD 2>$null).Trim()
    }

    $dotNetCheckResult = Test-DotNetVersion
    if (-not $dotNetCheckResult.Success) {
        $logFile = Get-LogFile # Create log ONLY on failure
        "Prerequisite check failed: $($dotNetCheckResult.Message)" | Out-File -FilePath $logFile -Append
        Invoke-Item $logFile
        Read-Host "Prerequisite check failed. ENTER to exit..."
        return
    }

    $exit = $false
    while (-not $exit) {
        # Screen management is now handled here to guarantee a clean slate for the menu.
        Clear-Host
        [System.Console]::SetCursorPosition(0, 0)
        Show-Menu -LogFile $global:logFile
        $choice = Read-Host

        # Defer log creation until a choice that needs logging is made.
        if ($choice.ToLower() -ne 'q') {
            $logFile = Get-LogFile
            $choiceKey = $choice.ToUpper()
            if ($global:menuItems.ContainsKey($choiceKey)) {
                $description = $global:menuItems[$choiceKey]
                "User selected option: '$choice' ($description)" | Out-File -FilePath $logFile -Append
            }
            else {
                "User selected invalid option: '$choice'" | Out-File -FilePath $logFile -Append
            }
        }

        switch ($choice.ToLower()) {
            "1" { Start-BuildAndRun -Configuration "Debug"; Read-Host "ENTER to continue..." }
            "2" { Start-BuildAndRun -Configuration "Release"; Read-Host "ENTER to continue..." }
            "3" { Watch-And-Run }
            "4" { Invoke-UnitTests; Read-Host "ENTER to continue..." }
            "5" { Publish-Portable; Read-Host "ENTER to continue..." }
            "6" { Build-ProductionPackage; Read-Host "ENTER to continue..." }
            "7" { Get-OutdatedPackages; Read-Host "ENTER to continue..." }
            "8" { Restore-NuGetPackages; Start-Sleep -Seconds 2 }
            
            "a" { Invoke-ItemSafely -Path (Join-Path $solutionRoot "Output") -ItemType "Output folder" }
            "b" { Invoke-ItemSafely -Path $solutionFile -ItemType "Solution file" }
            "c" { Remove-BuildOutput; Start-Sleep -Seconds 2 }
            "d" { Clear-Logs; Start-Sleep -Seconds 2 }
            "e" { Update-VersionNumber; Start-Sleep -Seconds 2 }
            "f" { Invoke-ItemSafely -Path (Join-Path $global:solutionRoot "appveyor.yml") -ItemType "appveyor.yml file" }
            "g" { Open-UserDataFolder }
            "h" { Open-LatestLogFile }

            "q" { $exit = $true }
            default { Write-Host "Invalid option." -ForegroundColor Red; Start-Sleep -Seconds 2 }
        }
    }
}
catch {
    $logFile = Get-LogFile
    $errorMsg = "A script-terminating error occurred.`nERROR: $($_.Exception.Message)`n$($_ | Format-List * -Force | Out-String)"
    $errorMsg | Out-File -FilePath $logFile -Append
    Write-Host "ERROR! Something went wrong. See log for details: $logFile" -ForegroundColor Red
    Invoke-Item $logFile
    Read-Host "ENTER to exit..."
}
finally {
    Write-Host "Exiting script."
}