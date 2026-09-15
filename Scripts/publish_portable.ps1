[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory)]
    [string]$ArtifactsDirectory,

    [string]$Configuration = "Release",

    [string]$DotNetPath = "dotnet",

    [string]$Version = "dev",

    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$LogPath
    )

    & $DotNetPath @Arguments 2>&1 | Tee-Object -LiteralPath $LogPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Reset-Directory {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.Length -lt 20 -or $fullPath -eq [IO.Path]::GetPathRoot($fullPath)) {
        throw "Refusing to replace unsafe directory: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        [IO.Directory]::Delete($fullPath, $true)
    }
    [void][IO.Directory]::CreateDirectory($fullPath)
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve the source commit."
}
$sourceStatus = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the source working-tree status."
}
$sourceDirty = $sourceStatus.Count -gt 0
if ($sourceDirty -and -not $AllowDirty) {
    throw "The source working tree is dirty. Commit the exact source before packaging, or pass -AllowDirty for a development-only artifact."
}
if ($sourceDirty -and $AllowDirty -and $Version -notmatch "^(dev|ci|local)([._-].*)?$") {
    throw "Dirty source requires dev, ci, or local, optionally followed by '.', '_', or '-' and a suffix."
}

$stateBuilder = [Text.StringBuilder]::new()
[void]$stateBuilder.Append((& git -C $repositoryRoot diff --binary HEAD | Out-String))
$sourceUntracked = @(& git -C $repositoryRoot ls-files --others --exclude-standard)
foreach ($relativePath in $sourceUntracked | Sort-Object) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        [void]$stateBuilder.AppendLine($relativePath)
        [void]$stateBuilder.AppendLine(
            (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant())
    }
}
$stateBytes = [Text.Encoding]::UTF8.GetBytes($stateBuilder.ToString())
$sourceStateSha256 = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($stateBytes)).ToLowerInvariant()

$artifactsRoot = [IO.Path]::GetFullPath($ArtifactsDirectory)
$buildRoot = Join-Path $artifactsRoot "build\$RuntimeIdentifier"
$publishRoot = Join-Path $artifactsRoot "publish\$RuntimeIdentifier\FlowLauncher"
$packageRoot = Join-Path $artifactsRoot "packages"
$logRoot = Join-Path $artifactsRoot "logs\$RuntimeIdentifier"
$lockRoot = Join-Path $artifactsRoot "locks\$RuntimeIdentifier"
$reportRoot = Join-Path $artifactsRoot "reports\$RuntimeIdentifier"

Reset-Directory $buildRoot
Reset-Directory $publishRoot
Reset-Directory $logRoot
Reset-Directory $lockRoot
[void][IO.Directory]::CreateDirectory($packageRoot)
[void][IO.Directory]::CreateDirectory($reportRoot)

$applicationProject = Join-Path $repositoryRoot "Flow.Launcher\Flow.Launcher.csproj"
$pluginProjects = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "Plugins") -Filter "*.csproj" -File -Recurse |
    Where-Object { $_.Directory.Name.StartsWith("Flow.Launcher.Plugin.", [StringComparison]::Ordinal) } |
    Sort-Object FullName)
if ($pluginProjects.Count -eq 0) {
    throw "No bundled plugin projects were found."
}

$commonBuildArguments = @(
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--nologo",
    "--verbosity", "minimal",
    "/p:FlowLauncherRuntimeIdentifier=$RuntimeIdentifier",
    "/p:FlowLauncherArchitectureLockRoot=$lockRoot",
    "/p:FlowLauncherOutputRoot=$buildRoot"
)

Invoke-DotNet `
    -Arguments (@("build", $applicationProject) + $commonBuildArguments) `
    -LogPath (Join-Path $logRoot "Flow.Launcher-build.log")

foreach ($pluginProject in $pluginProjects) {
    Invoke-DotNet `
        -Arguments (@("build", $pluginProject.FullName) + $commonBuildArguments) `
        -LogPath (Join-Path $logRoot "$($pluginProject.BaseName)-build.log")
}

Invoke-DotNet `
    -Arguments @(
        "publish",
        $applicationProject,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "--nologo",
        "--verbosity", "minimal",
        "--output", $publishRoot,
        "/p:FlowLauncherRuntimeIdentifier=$RuntimeIdentifier",
        "/p:FlowLauncherArchitectureLockRoot=$lockRoot",
        "/p:FlowLauncherOutputRoot=$buildRoot",
        "/p:PublishReadyToRun=false",
        "/p:PublishTrimmed=false"
    ) `
    -LogPath (Join-Path $logRoot "Flow.Launcher-publish.log")

$builtPluginsRoot = Join-Path $buildRoot "Plugins"
$publishedPluginsRoot = Join-Path $publishRoot "Plugins"
if (-not (Test-Path -LiteralPath $builtPluginsRoot)) {
    throw "Bundled plugin output is missing: $builtPluginsRoot"
}
Copy-Item -LiteralPath $builtPluginsRoot -Destination $publishedPluginsRoot -Recurse

$temporaryPluginDirectories = @(Get-ChildItem -LiteralPath $publishedPluginsRoot -Directory |
    Where-Object { $_.Name -like "*_wpftmp*" })
if ($temporaryPluginDirectories.Count -gt 0) {
    throw "Temporary WPF plugin directories entered the package: $($temporaryPluginDirectories.Name -join ', ')"
}

$publishedPluginDirectories = @(Get-ChildItem -LiteralPath $publishedPluginsRoot -Directory)
if ($publishedPluginDirectories.Count -ne $pluginProjects.Count) {
    throw "Expected $($pluginProjects.Count) bundled plugins, found $($publishedPluginDirectories.Count)."
}

foreach ($pluginDirectory in $publishedPluginDirectories) {
    $metadataPath = Join-Path $pluginDirectory.FullName "plugin.json"
    if (-not (Test-Path -LiteralPath $metadataPath)) {
        throw "Plugin metadata is missing: $metadataPath"
    }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $entryPoint = Join-Path $pluginDirectory.FullName $metadata.ExecuteFileName
    if (-not (Test-Path -LiteralPath $entryPoint)) {
        throw "Plugin entry point is missing: $entryPoint"
    }
}

Get-ChildItem -LiteralPath $publishRoot -Filter "*.pdb" -File -Recurse |
    ForEach-Object { [IO.File]::Delete($_.FullName) }
[void][IO.Directory]::CreateDirectory((Join-Path $publishRoot "UserData"))

$architectureReport = Join-Path $reportRoot "architecture.json"
& (Join-Path $PSScriptRoot "validate_package_architecture.ps1") `
    -RootPath $publishRoot `
    -RuntimeIdentifier $RuntimeIdentifier `
    -ReportPath $architectureReport
if ($LASTEXITCODE -ne 0) {
    throw "Package architecture validation failed."
}

$safeVersion = $Version -replace "[^A-Za-z0-9._-]", "-"
$zipPath = Join-Path $packageRoot "Flow-Launcher-$safeVersion-$RuntimeIdentifier-Portable.zip"
if (Test-Path -LiteralPath $zipPath) {
    [IO.File]::Delete($zipPath)
}
[IO.Compression.ZipFile]::CreateFromDirectory(
    $publishRoot,
    $zipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $true)

$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Update)
try {
    if (-not $archive.GetEntry("FlowLauncher/UserData/")) {
        [void]$archive.CreateEntry("FlowLauncher/UserData/")
    }
} finally {
    $archive.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [pscustomobject]@{
    SchemaVersion = 1
    GeneratedAt = [DateTimeOffset]::Now.ToString("o")
    RuntimeIdentifier = $RuntimeIdentifier
    Configuration = $Configuration
    SourceCommit = $sourceCommit
    SourceDirty = $sourceDirty
    DevelopmentArtifact = $sourceDirty
    SourceStatus = $sourceStatus
    SourceUntracked = $sourceUntracked
    SourceStateSha256 = $sourceStateSha256
    DotNetVersion = (& $DotNetPath --version).Trim()
    PluginCount = $publishedPluginDirectories.Count
    PublishRoot = $publishRoot
    ArchitectureReport = $architectureReport
    PortableZip = $zipPath
    PortableZipSize = (Get-Item -LiteralPath $zipPath).Length
    PortableZipSha256 = $zipHash
}
$manifestPath = Join-Path $reportRoot "package-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
"$zipHash  $([IO.Path]::GetFileName($zipPath))" |
    Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii

$manifest | ConvertTo-Json -Depth 5
