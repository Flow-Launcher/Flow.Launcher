[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RootPath,

    [Parameter(Mandatory)]
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory)]
    [string]$ReportPath,

    [string[]]$AllowedMismatchPatterns = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Reflection.Metadata

function Get-NormalizedPath {
    param([Parameter(Mandatory)][string]$Path)

    return [IO.Path]::GetFullPath($Path)
}

$root = Get-NormalizedPath $RootPath
if (-not (Test-Path -LiteralPath $root)) {
    throw "Package root does not exist: $root"
}

$expectedMachines = switch ($RuntimeIdentifier) {
    "win-x64" { @("Amd64") }
    "win-arm64" { @("Arm64", "Arm64EC") }
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object { $_.Extension -in @(".exe", ".dll") })
$results = @()
$validationErrors = @()
if ($files.Count -eq 0) {
    $validationErrors += "No EXE or DLL files were found under the package root."
}
if (-not (Test-Path -LiteralPath (Join-Path $root "Flow.Launcher.exe") -PathType Leaf)) {
    $validationErrors += "Required application entry point is missing: Flow.Launcher.exe"
}

foreach ($file in $files) {
    $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName)
    $stream = [IO.File]::Open($file.FullName, "Open", "Read", "ReadWrite")
    $reader = $null
    try {
        $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        $headers = $reader.PEHeaders
        if (-not $headers.PEHeader) {
            throw "Missing PE header"
        }

        $machine = $headers.CoffHeader.Machine.ToString()
        $corFlags = if ($headers.CorHeader) { $headers.CorHeader.Flags } else { $null }
        $isIlOnly = $corFlags -and $corFlags.HasFlag(
            [System.Reflection.PortableExecutable.CorFlags]::ILOnly)
        $requires32Bit = $corFlags -and $corFlags.HasFlag(
            [System.Reflection.PortableExecutable.CorFlags]::Requires32Bit)
        $isAnyCpu = $reader.HasMetadata -and $isIlOnly -and $machine -eq "I386" -and -not $requires32Bit
        $allowedByPattern = @($AllowedMismatchPatterns |
            Where-Object { $relativePath -like $_ }).Count -gt 0
        $machineMatches = $expectedMachines -contains $machine
        $valid = $isAnyCpu -or $machineMatches -or $allowedByPattern

        $classification = if ($isAnyCpu) {
            "managed-anycpu"
        } elseif ($reader.HasMetadata) {
            "managed-$($machine.ToLowerInvariant())"
        } else {
            "native-$($machine.ToLowerInvariant())"
        }

        $results += [pscustomobject]@{
            Path = $relativePath
            Size = $file.Length
            Machine = $machine
            HasMetadata = $reader.HasMetadata
            CorFlags = if ($corFlags) { $corFlags.ToString() } else { $null }
            Classification = $classification
            AllowedByPattern = $allowedByPattern
            Valid = $valid
            Error = $null
        }
    } catch {
        $results += [pscustomobject]@{
            Path = $relativePath
            Size = $file.Length
            Machine = $null
            HasMetadata = $null
            CorFlags = $null
            Classification = "invalid-pe"
            AllowedByPattern = $false
            Valid = $false
            Error = $_.Exception.Message
        }
    } finally {
        if ($reader) {
            $reader.Dispose()
        }
        $stream.Dispose()
    }
}

$invalid = @($results | Where-Object { -not $_.Valid })
$invalidCount = $invalid.Count + $validationErrors.Count
$report = [pscustomobject]@{
    SchemaVersion = 1
    GeneratedAt = [DateTimeOffset]::Now.ToString("o")
    RootPath = $root
    RuntimeIdentifier = $RuntimeIdentifier
    ExpectedMachines = $expectedMachines
    AllowedMismatchPatterns = $AllowedMismatchPatterns
    FileCount = $results.Count
    InvalidCount = $invalidCount
    ValidationErrors = $validationErrors
    Summary = @($results |
        Group-Object Classification |
        Sort-Object Name |
        ForEach-Object {
            [pscustomobject]@{
                Classification = $_.Name
                Count = $_.Count
            }
        })
    Files = $results
}

$reportDirectory = Split-Path -Parent $ReportPath
if ($reportDirectory) {
    [void][IO.Directory]::CreateDirectory($reportDirectory)
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8
$results | Export-Csv -LiteralPath ([IO.Path]::ChangeExtension($ReportPath, ".csv")) -NoTypeInformation -Encoding utf8

$report | Select-Object RuntimeIdentifier, FileCount, InvalidCount, Summary | ConvertTo-Json -Depth 5

if ($invalidCount -gt 0) {
    $invalid | Select-Object Path, Machine, Classification, Error | Format-Table -AutoSize
    if ($validationErrors.Count -gt 0) {
        $validationErrors | ForEach-Object { Write-Warning $_ }
    }
    throw "$invalidCount package architecture validation errors were found for $RuntimeIdentifier."
}
