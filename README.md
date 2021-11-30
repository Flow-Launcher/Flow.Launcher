<p align="center">
  <a href="https://flow-launcher.github.io">
	<img width="700px" src="../docs/assets/1_flow.png">
  </a>
</p>
![Maintenance](https://img.shields.io/maintenance/yes/3000)
[![Build status](https://ci.appveyor.com/api/projects/status/32r7s2skrgm9ubva?svg=true&retina=true)](https://ci.appveyor.com/project/JohnTheGr8/flow-launcher/branch/dev)
[![Github All Releases](https://img.shields.io/github/downloads/Flow-Launcher/Flow.Launcher/total.svg)](https://github.com/Flow-Launcher/Flow.Launcher/releases)
![GitHub Release Date](https://img.shields.io/github/release-date/Flow-Launcher/Flow.Launcher)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Flow-Launcher/Flow.Launcher)](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest)
[![Documentation](https://img.shields.io/badge/Documentation-7389D8)](https://flow-launcher.github.io/docs)
[![Discord](https://img.shields.io/discord/727828229250875472?color=7389D8&labelColor=6A7EC2&label=Community&logo=discord&logoColor=white)](https://discord.gg/AvgAQgh)

Search everything from applications, files, bookmarks, YouTube, Twitter and more. All from the comfort of your keyboard without ever touching the mouse.

[<img width="12px" src="https://user-images.githubusercontent.com/26427004/104119722-9033c600-5385-11eb-9d57-4c376862fd36.png"> **SOFTPEDIA EDITOR'S PICK**](https://www.softpedia.com/get/System/Launchers-Shutdown-Tools/Flow-Launcher.shtml)

## 🎉 New Features in 1.9

- All New Design. New Themes, New Setting Window. Animation & Sound Effect, Color Scheme aka Dark Mode.
- New Plugins - Window Setting, Visual Studio Code, Favorites, Window Services, Window Startup
- Plugin Store, Game Mode
- Global 3rd Party File Manager & Default Web Browser Setting
- New Result Order rank, Reduce Program Size.
- Do you want more? Check the changelogs.

---

<h4 align="center">
  <a href="#Getting-Started">Getting Started</a> •  <a href="#Features">Features</a> • <a href="#Plugins">Plugins</a> •
  <a href="#QuestionsSuggestions">Questions/Suggestions</a> •
  <a href="#Development">Development</a> •
  <a href="https://flow-launcher.github.io/docs">Documentation</a>
</h4>

---

## 🚗 Getting Started

### Installation

| [Windows 7+ installer](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Setup.exe) | [Portable](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Portable.zip) | `WinGet install "Flow Launcher"` |
| :----------------------------------------------------------: | :----------------------------------------------------------: | :------------------------------: |

> Windows may complain about security due to code not being signed, this will be completed at a later stage. If you downloaded from this repo, you are good to continue the set up. 

And You can download [Early Access Version]: https://github.com/Flow-Launcher/Flow.Launcher/discussions/640

## Features

### Applications & Files

- Flow searches files and contents via Windows Index Search
- Search for file contents.
- Support search using environment variable paths.

### Web Search & Open URL

### Browser Bookmarks

### System Commands

![4_SystemCommand](C:\DEV\docs\assets\4_SystemCommand.png)

- Provides System related commands. shutdown, lock, settings, etc.
- System command list

### Calculator

![2_Search_App_File](C:\DEV\docs\assets\2_Search_App_File.png)

- Do mathematical calculations and copy the result to clipboard. 

### Shell Command

![3_Shell](C:\DEV\docs\assets\3_Shell.png)

- Run batch and PowerShell commands as Administrator or a different user.
- Ctrl+Enter to Run as Administrator.

### Explorer



### Window Setting & Control Panel

![7_ControlPanelandsettings](C:\DEV\docs\assets\7_ControlPanelandsettings.png)

- Search within Window Settings. (Default Action Keyword is "$")
- Also Search within Control Panel.

### Explorer

### Priority

- Prioritise the order of each plugin's results.

### Customization

![darkmode](C:\DEV\docs\assets\darkmode.png)

- Window size adjustment, animation, and sound setting are possible.
- There are various themes and you can make it yourself.
- Color Scheme (aka Dark Mode) is available.

### Language

- Support languages from Chinese to Italian and more.
- Support PinYin 
- You can translation support this project in [Crowdin](https://crowdin.com/project/flow-launcher)

### Portable

- Fully portable.
- Type `flow user data` to open your saved user settings folder. They are located at:
  - If using roaming: `%APPDATA%\FlowLauncher`
  - If using portable, by default: `%localappdata%\FlowLauncher\app-<VersionOfYourFlowLauncher>\UserData` 
  - Type `open log location` to open your logs folder, they are saved along with your user settings folder.

### Game Mode
![gamemode](C:\DEV\docs\assets\gamemode.png)
- Suspend Hotkey when you playing game.
- Surely, You can ignore hotkeys in fullscreen game in settings.

----

## Plugins

- Support wide range of plugins. Visit [here](https://flow-launcher.github.io/docs/#/plugins) for our plugin portfolio.
- If you are using Python plugins, flow will prompt to either select the location or allow Python (Embeddable) to be automatic downloaded for use.
- If you are keen to write your own plugin for flow, please take a look at our plugin development documentation for [C#](https://flow-launcher.github.io/docs/#/develop-dotnet-plugins) or [Python](

### Plugin Install

![pm_install](C:\DEV\docs\assets\pm_install.png)

- Install/Uninstall/Update plugins: in the search window, type `pm` `install`/`uninstall`/`update` + the plugin name.

- If you developer, You can download plugin by direct URL. `pm install <URL>`

### Plugin Store

![pluginstore](C:\DEV\docs\assets\pluginstore.png)

- Simply you can check the full plugin list and Quick install in plugin store menu in setting.

### Plugin - Everything

- to use **Everything**: `pm install everything`.

### Plugin - Ha Commander

----

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
| <kbd>Ctrl</kbd> + <kbd>h</kbd>                               | Open Query History                           |


## System Command List

| Command                | Description                                                  |
| ---------------------- | ------------------------------------------------------------ |
| Shutdown               | Shutdown computer                                            |
| Restart                | Restart computer                                             |
| Restart with advance   | Restart the computer with Advanced Boot option for safe and debugging modes |
| Log off                | Log off                                                      |
| Lock                   | Lock computer                                                |
| Sleep                  | Put computer to sleep                                        |
| Hibernate              | Hibernate computer                                           |
| Empty Recycle Bin      | Empty recycle bin                                            |
| Exit                   | Close Flow Launcher                                          |
| Save Settings          | Save all Flow Launcher settings                              |
| Restart Flow Launcher  | Restart Flow Launcher                                        |
| Settings               | Tweak this app                                               |
| Reload Plugin Data     | Refreshes plugin data with new content                       |
| Check For Update       | Check for new Flow Launcher update                           |
| Open Log Location      | Open Flow Launcher's log location                            |
| Flow Launcher Tip      | Visit Flow Launcher's documentation for more help and how to use tips |
| Flow Launcher UserData | Open the location where Flow Launcher's settings are stored  |

----






- Save file or folder locations for quick access.

- 

[More tips](https://flow-launcher.github.io/docs/#/usage-tips)



----

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

- Flow Launcher's target framework is .Net 5

- Install Visual Studio 2019

- Install .Net 5 SDK via Visual Studio installer or manually from [here](https://dotnet.microsoft.com/download/dotnet/thank-you/sdk-5.0.103-windows-x64-installer)
