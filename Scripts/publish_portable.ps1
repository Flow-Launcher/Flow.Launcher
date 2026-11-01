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
    "/p:FlowLauncherOutputRoot=$buildRoot",
    "/p:GeneratePackageOnBuild=false"
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
        "/p:GeneratePackageOnBuild=false",
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

$explorerSdkRoot = Join-Path $publishedPluginsRoot "Flow.Launcher.Plugin.Explorer\EverythingSDK"
$expectedSdkArchitecture = if ($RuntimeIdentifier -eq "win-arm64") { "arm64" } else { "x64" }
$unexpectedSdkArchitecture = if ($RuntimeIdentifier -eq "win-arm64") { "x64" } else { "arm64" }
$expectedSdkRoot = Join-Path $explorerSdkRoot $expectedSdkArchitecture
$sdkMetadata = @{
    "win-x64" = @{
        "Everything.dll" = @{
            Sha256 = "ae856af0c30068d9ba4c65d64ec3b30fda85c62914141bcd79a6381074a84948"
            SignerThumbprint = "B5B6468C781744765A590C0FE13AA418FC3335D1"
        }
        "Everything3.dll" = @{
            Sha256 = "be25b01c73bbf359b50ddf30255133225f93b4bc40a8d208173319373bcdaa5c"
            SignerThumbprint = "6C8A3919279E9756765978716EB07C8052F5D1DE"
        }
    }
    "win-arm64" = @{
        "Everything.dll" = @{
            Sha256 = "8531ea393677dd8fd37bed7420ac93344cd458b9a1324ba65c4a75d024d61886"
            SignerThumbprint = "6C8A3919279E9756765978716EB07C8052F5D1DE"
        }
        "Everything3.dll" = @{
            Sha256 = "0ef26560d1c0224686e67134ada57171f40f326a872ee8a1f2200e973f49f871"
            SignerThumbprint = "6C8A3919279E9756765978716EB07C8052F5D1DE"
        }
    }
}
foreach ($sdkName in $sdkMetadata[$RuntimeIdentifier].Keys) {
    $sdkPath = Join-Path $expectedSdkRoot $sdkName
    if (-not (Test-Path -LiteralPath $sdkPath -PathType Leaf)) {
        throw "Required $expectedSdkArchitecture Everything SDK is missing: $sdkPath"
    }
    $expectedMetadata = $sdkMetadata[$RuntimeIdentifier][$sdkName]
    $actualHash = (Get-FileHash -LiteralPath $sdkPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedMetadata.Sha256) {
        throw "Everything SDK hash mismatch for $sdkPath. Expected $($expectedMetadata.Sha256), found $actualHash."
    }
    $signature = Get-AuthenticodeSignature -FilePath $sdkPath
    if ($signature.Status -ne "Valid" -or
        -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $expectedMetadata.SignerThumbprint) {
        throw "Everything SDK signature validation failed for $sdkPath."
    }
}
$unexpectedSdkRoot = Join-Path $explorerSdkRoot $unexpectedSdkArchitecture
if (Test-Path -LiteralPath $unexpectedSdkRoot) {
    throw "Unexpected $unexpectedSdkArchitecture Everything SDK entered the $RuntimeIdentifier package."
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
