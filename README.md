<p align="center">
  <img width="500px" src="https://github.com/Flow-Launcher/Flow.Launcher/blob/5ba4514f31e624c679628d4dfe89036c0e24006c/Doc/Logo/resources/flow-header-square-transparent.png">
</p>

![Maintenance](https://img.shields.io/maintenance/yes/2020)
[![Build status](https://ci.appveyor.com/api/projects/status/32r7s2skrgm9ubva?svg=true&retina=true)](https://ci.appveyor.com/project/JohnTheGr8/flow-launcher/branch/dev)
[![Github All Releases](https://img.shields.io/github/downloads/Flow-Launcher/Flow.Launcher/total.svg)](https://github.com/Flow-Launcher/Flow.Launcher/releases)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Flow-Launcher/Flow.Launcher)](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest)
![GitHub Release Date](https://img.shields.io/github/release-date/Flow-Launcher/Flow.Launcher)
[![Discord](https://img.shields.io/discord/727828229250875472?color=7389D8&labelColor=6A7EC2&label=Community&logo=discord&logoColor=white)](https://discord.gg/AvgAQgh)

Flow Launcher. Dedicated to make your workflow flow more seamlessly. Aimed at being more than an app launcher, it searches, integrates and expands on functionalities. Flow will continue to evolve, designed to be open and built with the community at heart.

<sub>Remember to star it, flow will love you more :)</sub>

## Features
![The Flow](https://user-images.githubusercontent.com/26427004/82151677-fa9c7100-989f-11ea-9143-81de60aaf07d.gif)

- Search everything from applications, files, bookmarks, YouTube, Twitter and more. All from the comfort of your keyboard without ever touching the mouse.
- Search for file contents
- Support search using environment variable paths
- Run batch and PowerShell commands as Administrator or a different user.
- Support languages from Chinese to Italian and more.
- Support of wide range of plugins.
- Fully portable.

## Running Flow Launcher

| [Windows 7 and up](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest)
| ------------- |

Windows may complain about security due to code not being signed, this will be completed at a later stage. If you downloaded from this repo, you are good to continue the set up. 

**Integrations:**
  - If you use python plugins, install [python3](https://www.python.org/downloads/): `.exe` installer + add it to `%PATH%` or set it in flow's settings
  - Flow searches files and contents via Windows Index Search, to use Everything, download the plugin [here](https://github.com/Flow-Launcher/Flow.Launcher.Plugin.Everything/releases/latest)

**Usage**
- Open flow's search window: <kbd>Alt</kbd>+<kbd>Space</kbd> is the default hotkey
- Open context menu: <kbd>Ctrl</kbd>+<kbd>O</kbd>/<kbd>Shift</kbd>+<kbd>Enter</kbd>
- Cancel/Return to previous screen: <kbd>Esc</kbd>
- Install/Uninstall plugins: in the search window, type `wpm install/uninstall` + the plugin name
- Saved user settings are located:
  - If using roaming: `%APPDATA%\FlowLauncher`
  - If using portable, by default: `%localappdata%\FlowLauncher\app-<VersionOfYourFlowLauncher>\UserData` 
- Logs are saved along with your user settings folder

## Status

Flow is under heavy development, but the code base is stable, so contributions are very welcome. If you would like to help maintain it, please do not hesistate to get in touch.

## Contributing

We welcome all contributions. If you are unsure of a change you want to make, simply put an issue in for discussion, otherwise feel free to put in a pull request.

You will find the main goals of flow placed under Projects board, so feel free to contribute on that. If you would like to make small incremental changes, feel free to do so as well.

Get in touch if you like to join the Flow-Launcher Team and help build this great tool.

**Question/Suggestion**

Yes please, submit an issue to let us know.

**Join our community on [Discord](https://discord.gg/AvgAQgh)!**

## Developing/Debugging

Flow Launcher's target framework is .Net Core 3.1

Install Visual Studio 2019

Install .Net Core 3.1 SDK via Visual Studio installer or manually from [here](https://dotnet.microsoft.com/download/dotnet-core/thank-you/sdk-3.1.201-windows-x64-installer)

## Documentation

[Wiki](https://github.com/Flow-Launcher/Flow.Launcher/wiki)

## A history of the flow
Flow's roots came from a rebrand of the [JJW24/Wox fork](https://github.com/jjw24/Wox/issues/156) and WoX.

A big thank you and all credits to [Bao](https://github.com/bao-qian), the author of WoX, and its contrbutors for all the amazing work.

The JJW24/Wox fork started adding new changes on top of main WoX repo's code base from release v1.3.524. Flow is a continuation of the work from JJW24/Wox
