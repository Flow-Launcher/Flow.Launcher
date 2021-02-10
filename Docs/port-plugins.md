# Porting Wox Plugins

Note:
- When porting, please keep the author's commit history
- Flow Launcher targets .Net Core 3.1, so plugins should also be upgraded to keep the continuity of future developments

Steps:
1. to start off, you can fork/create a new repo, either way the project's commit history must be kept. If it's forked, you can just start updating it. If it's a new repo, do this by first cloning the repo, then add your new repo as a new repo remote, remove the original remote and then push to it.
2. use try convert tool from https://github.com/dotnet/try-convert
3. try-convert -w path-to-folder-or-solution-or-project
4. May need to fix on the project file, a good template to follow is the [Explorer plugin](https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/Plugins/Flow.Launcher.Plugin.Explorer/Flow.Launcher.Plugin.Explorer.csproj) project:
	- fix <TargetFramework> to netcoreapp3.1
	- set the output location as 'Output\Release\<name of the project>'
	- add <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> and <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
	- bump version to 2.0.0 and fix up any missing attributes if neccessary
5. update code and fix plugin's setting layout if neccessary
6. update readme to indicate where this port is from and the original author of the project