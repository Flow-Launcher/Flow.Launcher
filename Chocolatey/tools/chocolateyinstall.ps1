$ErrorActionPreference = 'Stop';

$packageName  = 'FlowLauncher'
$toolsDir     = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$packageArgs = @{
  packageName   = $packageName
  fileType      = 'exe'
  url           = 'https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Setup.exe'
  silentArgs    = "/S"
  validExitCodes= @(0)
}

Install-ChocolateyPackage @packageArgs

