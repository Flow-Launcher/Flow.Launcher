<p align="center">
  <a href="https://flow-launcher.github.io">
	<img width="500px" src="../docs/assets/1_flow.png">
  </a>
</p>
![Maintenance](https://img.shields.io/maintenance/yes/3000)
[![Build status](https://ci.appveyor.com/api/projects/status/32r7s2skrgm9ubva?svg=true&retina=true)](https://ci.appveyor.com/project/JohnTheGr8/flow-launcher/branch/dev)
[![Github All Releases](https://img.shields.io/github/downloads/Flow-Launcher/Flow.Launcher/total.svg)](https://github.com/Flow-Launcher/Flow.Launcher/releases)
![GitHub Release Date](https://img.shields.io/github/release-date/Flow-Launcher/Flow.Launcher)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Flow-Launcher/Flow.Launcher)](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest)
[![Documentation](https://img.shields.io/badge/Documentation-7389D8)](https://flow-launcher.github.io/docs)
[![Discord](https://img.shields.io/discord/727828229250875472?color=7389D8&labelColor=6A7EC2&label=Community&logo=discord&logoColor=white)](https://discord.gg/AvgAQgh)

Flow Launcher. Dedicated to make your workflow flow more seamlessly. Aimed at being more than an app launcher, it searches, integrates and expands on functionalities. Flow will continue to evolve, designed to be open and built with the community at heart.

<sub>Remember to star it, flow will love you more :)</sub>

[<img width="12px" src="https://user-images.githubusercontent.com/26427004/104119722-9033c600-5385-11eb-9d57-4c376862fd36.png"> **SOFTPEDIA EDITOR'S PICK**](https://www.softpedia.com/get/System/Launchers-Shutdown-Tools/Flow-Launcher.shtml)

## 🚗 Getting Started

### Installation

| [Windows 7+ installer](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Setup.exe) | [Portable](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Portable.zip) | `WinGet install "Flow Launcher"` |
| :----------------------------------------------------------: | :----------------------------------------------------------: | :------------------------------: |

> Windows may complain about security due to code not being signed, this will be completed at a later stage. If you downloaded from this repo, you are good to continue the set up. 

## 🎉 New Features in 1.9

- All New Design. New Themes, New Setting Window. Animation & Sound Effect, Color Scheme aka Dark Mode.
- New Plugins - Window Setting, Visual Studio Code, Favorites, Window Services, Window Startup
- Plugin Store, Game Mode
- Global 3rd Party File Manager & Default Web Browser Setting
- New Result Order rank, Reduce Program Size
- ......More

## Features

### Applications & Files

### Web Search & Open URL

### Browser Bookmarks

### System Commands

### Calculator

### Shell Command

### Window Setting & Control Panel

### Priority

Prioritise the order of each plugin's results.

### Customization

## Plugins

### Plugin Install & Plugin Store

### Plugin - Everything

### Plugin - Ha Commander

## Hotkeys

| Hotkey                                                       | Description                                  |
| ------------------------------------------------------------ | -------------------------------------------- |
| <kbd>Alt</kbd>+ <kbd>Space</kbd>                             | Open Search Box (Default and Configurable)   |
| <kbd>Enter</kbd>                                             | Execute                                      |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Enter</kbd>            | Run As Admin                                 |
| <kbd>↑</kbd><kbd>↓</kbd>                                     | Scroll up & Down                             |
| <kbd>←</kbd><kbd>→</kbd>                                     | Back to Result / Open Context Menu           |
| <kbd>Ctrl</kbd> +<kbd>o</kbd> , <kbd>Shift</kbd> +<kbd>Enter</kbd> | Open Context Menu                            |
| <kbd>Esc</kbd>                                               | Back to Result & Close                       |
| <kbd>Ctrl</kbd> +<kbd>i</kbd>                                | Open Setting Window                          |
| <kbd>F5</kbd>                                                | Reload All Plugin Data & Window Search Index |



- Search everything from applications, files, bookmarks, YouTube, Twitter and more. All from the comfort of your keyboard without ever touching the mouse.
- Search for file contents.
- Do mathematical calculations and copy the result to clipboard. 
- Support search using environment variable paths.
- Run batch and PowerShell commands as Administrator or a different user.
- Support languages from Chinese to Italian and more.
- Support wide range of plugins.
- Prioritise the order of each plugin's results.
- Save file or folder locations for quick access.
- Fully portable.

[<img width="12px" src="https://user-images.githubusercontent.com/26427004/104119722-9033c600-5385-11eb-9d57-4c376862fd36.png"> **SOFTPEDIA EDITOR'S PICK**](https://www.softpedia.com/get/System/Launchers-Shutdown-Tools/Flow-Launcher.shtml)



### Plugins

Flow searches files and contents via Windows Index Search, to use **Everything**: `pm install everything`.

If you are using Python plugins, flow will prompt to either select the location or allow Python (Embeddable) to be automatic downloaded for use.

Vist [here](https://flow-launcher.github.io/docs/#/plugins) for our plugin portfolio.

If you are keen to write your own plugin for flow, please take a look at our plugin development documentation for [C#](https://flow-launcher.github.io/docs/#/develop-dotnet-plugins) or [Python](https://flow-launcher.github.io/docs/#/develop-py-plugins)



---

<h4 align="center">
  <a href="#Features">Features</a> •
  <a href="#Getting-Started">Getting Started</a> •
  <a href="#QuestionsSuggestions">Questions/Suggestions</a> •
  <a href="#Development">Development</a> •
  <a href="https://flow-launcher.github.io/docs">Documentation</a>
</h4>


---

### Usage

- Open flow's search window: <kbd>Alt</kbd>+<kbd>Space</kbd> is the default hotkey.
- Open context menu: on the selected result, press <kbd>Ctrl</kbd>+<kbd>O</kbd>/<kbd>Shift</kbd>+<kbd>Enter</kbd>.
- Cancel/Return to previous screen: <kbd>Esc</kbd>.
- Install/Uninstall/Update plugins: in the search window, type `pm` `install`/`uninstall`/`update` + the plugin name.
- Type `flow user data` to open your saved user settings folder. They are located at:
  - If using roaming: `%APPDATA%\FlowLauncher`
  - If using portable, by default: `%localappdata%\FlowLauncher\app-<VersionOfYourFlowLauncher>\UserData` 
- Type `open log location` to open your logs folder, they are saved along with your user settings folder.

[More tips](https://flow-launcher.github.io/docs/#/usage-tips)

## Questions/Suggestions

Yes please, let us know in the [Q&A](https://github.com/Flow-Launcher/Flow.Launcher/discussions/categories/q-a) section. **Join our community on [Discord](https://discord.gg/AvgAQgh)!**

## Development

### Status

Flow is under heavy development, but the code base is stable, so contributions are very welcome. If you would like to help maintain it, please do not hesistate to get in touch.

### Contributing

We welcome all contributions. If you are unsure of a change you want to make, let us know in the [Discussions](https://github.com/Flow-Launcher/Flow.Launcher/discussions/categories/ideas), otherwise feel free to put in a pull request.

You will find the main goals of flow placed under the [Projects board](https://github.com/Flow-Launcher/Flow.Launcher/projects), so feel free to contribute on that. If you would like to make small incremental changes, feel free to do so as well.

Get in touch if you like to join the Flow-Launcher Team and help build this great tool.

### Developing/Debugging

Flow Launcher's target framework is .Net 5
Install Visual Studio 2019
Install .Net 5 SDK via Visual Studio installer or manually from [here](https://dotnet.microsoft.com/download/dotnet/thank-you/sdk-5.0.103-windows-x64-installer)
