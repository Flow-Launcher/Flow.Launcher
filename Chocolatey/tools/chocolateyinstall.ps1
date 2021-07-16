$ErrorActionPreference = 'Stop';

$packageName  = 'FlowLauncher'
$toolsDir     = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$packageArgs = @{
  packageName   = $packageName
  fileType      = 'exe'
  url           = 'https://github.com/Flow-Launcher/Flow.Launcher/releases/download/v1.0.0/Flow-Launcher-v1.0.0.exe'
  silentArgs    = "/S"
  validExitCodes= @(0)
}

Install-ChocolateyPackage @packageArgs

